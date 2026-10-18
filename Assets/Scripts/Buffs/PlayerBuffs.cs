using System.Collections.Generic;
using System.Text;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TalentTree;
using UnityEngine;

namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 玩家身上所有 Buff 的宿主。挂在 Player 上，负责三件事：
    /// <list type="number">
    /// <item>把各个来源（天赋 / 装扮 / 临时）翻译成 <see cref="BuffModifier"/> 喂给 <see cref="BuffAggregator"/>；</item>
    /// <item>在来源变化时重建，并广播 <see cref="BuffsRebuiltEvent"/>；</item>
    /// <item>给高频读取方（蟑螂、守卫、巢穴）提供便宜的 <see cref="CockroachBuffSnapshot"/>。</item>
    /// </list>
    ///
    /// 全局入口：<c>PlayerBuffs.Current</c>（= <see cref="GameManager.Player"/> 上的这一份）。
    /// 非玩家对象（蟑螂 / 守卫 / 巢穴）都从这里读，不要各自去找玩家。
    ///
    /// 现在的来源只有科技树天赋。装扮在 P4 接入：加一个 <see cref="BuffSourceKind.Dress"/> 的
    /// SetSource 调用即可，聚合器不用改。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBuffs : MonoBehaviour
    {
        [Tooltip("天赋来源。留空则从自己身上找 PlayerTalentTree")]
        [SerializeField] private PlayerTalentTree _talentTree;

        [Tooltip("装扮来源（三个装备槽）。留空则从自己身上找 PlayerBag")]
        [SerializeField] private PlayerBag _bag;

        [Tooltip("重建时把「配表里的 key 代码里没有」打到 Console。只在真的出现未知 key 时才打印")]
        [SerializeField] private bool _logWarnings = true;

        [Tooltip("每次重建打一条汇总日志：天赋等级数 / 各来源条数 / 几个关键系数。查「为什么没生效」时开它")]
        [SerializeField] private bool _logRebuild = true;

        private readonly BuffAggregator _aggregator = new BuffAggregator();
        private readonly List<string> _reportedUnknownKeys = new List<string>();
        private readonly List<string> _reportedWarnings = new List<string>();
        private readonly List<BuffModifier> _dressBuffer = new List<BuffModifier>();

        /// <summary>
        /// 天赋来源。**不要缓存这个对象**：`PlayerTalentTree.Effects` 可能在生命周期里被换掉，
        /// 缓存旧的那个就会出现「缓存的 resolver 没人 Invalidate，永远返回第一次的结果」。
        /// 每次重建都从 <c>_talentTree.Effects</c> 现拿，代价是每次天赋变化多一个小对象分配。
        /// </summary>
        private TalentBuffSource _talentSource;

        /// <summary>上一次广播出去的 Revision，用来避免重复发事件</summary>
        private int _publishedRevision = -1;

        private CockroachBuffSnapshot _cockroachSnapshot = CockroachBuffSnapshot.Identity;
        private int _snapshotRevision = -1;

        /// <summary>玩家身上的 Buff 宿主。场上没有玩家时返回 null。</summary>
        public static PlayerBuffs Current
        {
            get
            {
                PlayerBasic player = GameManager.Player;
                return player != null ? player.Buffs : null;
            }
        }

        /// <summary>聚合核心。需要细粒度查询时用它；常用几个见下面的快捷方法。</summary>
        public BuffAggregator Aggregator => _aggregator;

        /// <summary>每次 Buff 真的变了就 +1。消费方用它判断「我缓存的快照要不要重算」。</summary>
        public int Revision => _aggregator.Revision;

        /// <summary>
        /// 虫群 Buff 的快照。按 <see cref="Revision"/> 惰性重算，
        /// 所以蟑螂可以每帧随便拿（拿到的是同一个 struct，不分配）。
        /// </summary>
        public CockroachBuffSnapshot CockroachSnapshot
        {
            get
            {
                if (_snapshotRevision != _aggregator.Revision)
                {
                    _cockroachSnapshot = new CockroachBuffSnapshot(
                        _aggregator.GetFactor(BuffKey.CockroachSpeedPercent),
                        _aggregator.GetFactor(BuffKey.CockroachCarryPercent),
                        _aggregator.GetFactor(BuffKey.CockroachHatchPercent),
                        _aggregator.GetFactor(BuffKey.NestRangePercent),
                        _aggregator.Has(BuffKey.FlagGroupWallGnawing),
                        _aggregator.Has(BuffKey.FlagCockroachDeathFood));
                    _snapshotRevision = _aggregator.Revision;
                }

                return _cockroachSnapshot;
            }
        }

        #region 生命周期

        private void Awake()
        {
            if (_talentTree == null)
            {
                _talentTree = GetComponent<PlayerTalentTree>();
            }

            if (_bag == null)
            {
                _bag = GetComponent<PlayerBag>();
            }

            if (_talentTree == null)
            {
                Debug.LogWarning($"[PlayerBuffs:{name}]：没找到 PlayerTalentTree，天赋 Buff 不会生效", this);
            }

#if UNITY_EDITOR
            // key 表的下标对齐 / 重名之类的问题，在这里一次性喊出来，别等运行期算错了才发现
            List<string> problems = BuffKeys.Validate();
            for (int i = 0; i < problems.Count; i++)
            {
                Debug.LogError($"[PlayerBuffs] BuffKey 表有问题：{problems[i]}", this);
            }
#endif
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<TalentLevelChangedEvent>(HandleTalentLevelChanged);
            if (_bag != null)
            {
                _bag.EquippedChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<TalentLevelChangedEvent>(HandleTalentLevelChanged);
            if (_bag != null)
            {
                _bag.EquippedChanged -= Refresh;
            }
        }

        private void Start()
        {
            // 进场景时先把天赋算一遍：天赋进度本身可能是序列化下来的初始值
            Refresh();
        }

        private void HandleTalentLevelChanged(TalentLevelChangedEvent evt) => Refresh();

        #endregion

        #region 重建

        /// <summary>
        /// 重新收集所有来源并聚合。天赋等级变化 / 装备变化时自动调用；
        /// 以后临时效果变化也调这个。
        /// </summary>
        public void Refresh()
        {
            if (_talentTree != null)
            {
                // 每次都从 _talentTree.Effects 现拿 resolver，再把缓存打掉。
                // 绝对不能缓存在字段里：resolver 有可能被宿主换掉，
                // 那时缓存的这个就没人 Invalidate，会一直返回第一次算出的效果
                // （症状：天赋点了、日志也对，但数值永远不动）。
                _talentSource = new TalentBuffSource(_talentTree.Effects);
                _talentTree.Effects.Invalidate();

                _aggregator.SetSource(BuffSourceKind.Talent, _talentSource.Build());

                ReportUnknownKeys();
                ReportWarnings();
            }

            RefreshDress();

            LogRebuild();

            // 即使没有任何来源也要发一次：订阅方（UI、巢穴范围圈）需要一个「可以开始读了」的时机
            PublishIfChanged();
        }

        /// <summary>
        /// 装扮来源：三个装备槽 → 各自的 <see cref="DressData"/> → modifier。
        /// 由 <see cref="PlayerBag.EquippedChanged"/> 驱动，所以穿上 / 换下 / 丢弃都会重算。
        /// </summary>
        private void RefreshDress()
        {
            if (_bag == null)
            {
                return;
            }

            _dressBuffer.Clear();

            IReadOnlyList<PickableItem> equipped = _bag.EquippedItems;
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] is not DressItem dress || dress.Data == null)
                {
                    continue;
                }

                IReadOnlyList<DressBuffEntry> entries = dress.Data.Buffs;
                if (entries == null)
                {
                    continue;
                }

                for (int e = 0; e < entries.Count; e++)
                {
                    _dressBuffer.Add(BuffModifier.FromValue(entries[e].Key, entries[e].Value, BuffSourceKind.Dress));
                }
            }

            _dressBuffer.Sort(BuffModifier.CompareStable);
            _aggregator.SetSource(BuffSourceKind.Dress, _dressBuffer);
        }

        private void PublishIfChanged()
        {
            int revision = _aggregator.Revision;
            if (revision == _publishedRevision)
            {
                return;
            }

            _publishedRevision = revision;
            EventPipeline.Publish(new BuffsRebuiltEvent(revision));
        }

        /// <summary>把「资产里写了、代码里没有」的 key 打出来（每条只打一次，不刷屏）。</summary>
        private void ReportUnknownKeys()
        {
            if (!_logWarnings)
            {
                return;
            }

            IReadOnlyList<string> unknown = _talentSource.UnknownKeys;
            for (int i = 0; i < unknown.Count; i++)
            {
                string id = unknown[i];
                if (_reportedUnknownKeys.Contains(id))
                {
                    continue;
                }

                _reportedUnknownKeys.Add(id);
                Debug.LogWarning(
                    $"[PlayerBuffs]：天赋资产里有未知的效果 key「{id}」，这条效果不会生效。" +
                    "检查 BuffKey 表的稳定名字（Assets/Scripts/Buffs/BuffKeyTable.cs）", this);
            }
        }

        /// <summary>聚合器报的「op 和 key 类型不匹配」也打出来。</summary>
        private void ReportWarnings()
        {
            if (!_logWarnings)
            {
                return;
            }

            IReadOnlyList<string> warnings = _aggregator.Warnings;
            for (int i = 0; i < warnings.Count; i++)
            {
                if (_reportedWarnings.Contains(warnings[i]))
                {
                    continue;
                }

                _reportedWarnings.Add(warnings[i]);
                Debug.LogWarning(warnings[i], this);
            }
        }

        #endregion

        #region 排查

        /// <summary>
        /// 每次重建打一条汇总。<b>这是排查「配了效果却不生效」最快的入口</b>：
        /// - 天赋等级 = 0 → 天赋根本没解锁（效果只在解锁后生效）
        /// - 天赋条目 = 0 但等级 &gt; 0 → key 没解析出来（去看 <see cref="ReportUnknownKeys"/> 的 warning）
        /// - 条目 &gt; 0 但系数还是 ×1 → 配的是固定值 key，或者数值填在了 Tag 上
        /// </summary>
        private void LogRebuild()
        {
            if (!_logRebuild)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"[PlayerBuffs] rev={_aggregator.Revision}");
            sb.Append($" 天赋等级={CountActiveTalents()}");
            sb.Append($" 天赋条目={_aggregator.GetModifiers(BuffSourceKind.Talent).Count}");
            sb.Append($" 装扮条目={_aggregator.GetModifiers(BuffSourceKind.Dress).Count}");
            sb.Append($" | 移速×{_aggregator.GetFactor(BuffKey.PlayerMoveSpeedPercent):0.###}");
            sb.Append($" 巢穴范围×{_aggregator.GetFactor(BuffKey.NestRangePercent):0.###}");
            sb.Append($" 虫群速×{_aggregator.GetFactor(BuffKey.CockroachSpeedPercent):0.###}");
            sb.Append($" 孵化×{_aggregator.GetFactor(BuffKey.CockroachHatchPercent):0.###}");
            sb.Append($" 受伤×{_aggregator.GetFactor(BuffKey.PlayerDamageReduction):0.###}");

            AppendModifiers(sb, BuffSourceKind.Talent);
            AppendModifiers(sb, BuffSourceKind.Dress);

            Debug.Log(sb.ToString(), this);
        }

        private void AppendModifiers(StringBuilder sb, BuffSourceKind kind)
        {
            IReadOnlyList<BuffModifier> modifiers = _aggregator.GetModifiers(kind);
            if (modifiers.Count == 0)
            {
                return;
            }

            sb.Append($"\n  {kind}: ");
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(modifiers[i].ToString());
            }
        }

        /// <summary>当前解锁了几个天赋（等级 &gt; 0）。</summary>
        private int CountActiveTalents()
        {
            if (_talentTree == null)
            {
                return 0;
            }

            TalentTreeAsset asset = _talentTree.TalentTreeAsset;
            if (asset == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<TalentNodeData> nodes = asset.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                TalentData data = nodes[i]?.Data;
                if (data != null && _talentTree.GetLevel(data) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion

        #region 快捷查询

        /// <summary>把基准值过一遍 Buff。游戏逻辑应该都走这里。</summary>
        public float Apply(BuffKey key, float baseValue) => _aggregator.Apply(key, baseValue);

        /// <summary>整数版（四舍五入）。金币、格数、档位用。</summary>
        public int ApplyInt(BuffKey key, int baseValue) => _aggregator.ApplyInt(key, baseValue);

        /// <summary>只取乘数，不含固定值。</summary>
        public float GetFactor(BuffKey key) => _aggregator.GetFactor(key);

        /// <summary>只取固定值之和。</summary>
        public float GetFlat(BuffKey key) => _aggregator.GetFlat(key);

        /// <summary>能力 Tag 查询。</summary>
        public bool Has(BuffKey key) => _aggregator.Has(key);

        #endregion
    }
}
