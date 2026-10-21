using TapTapGameJam.Audio;
using System;
using TapTapGameJam.Buffs;
using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 黑盒：夜里按住 E 扫开，吐出一件道具。
    ///
    /// 出货规则（没配 <see cref="hidedItem"/> 时）：
    /// 1. 先按概率决定这一盒抽「普通」还是「稀有」——基准 <see cref="rareChance"/>，
    ///    幸运值按<b>比例</b>提升它（见 <see cref="LootRoll.RareChance"/>）；
    /// 2. 再从 <see cref="itemPool"/>（商品池数据资产）里对应稀有度的那一档<b>等概率</b>抽一个模板；
    /// 3. 抽到稀有物品时，扫描时间乘以 <see cref="rareScanTimeMultiplier"/>（默认翻倍）。
    ///
    /// 抽取发生在 <see cref="OnEnable"/>（盒子出现的瞬间），不是扫完的瞬间——
    /// 因为稀有物品的扫描时间要翻倍，玩家在扫的过程中就得看到更长的进度条。
    /// 这样也顺带堵住「扫一半取消重开、直到刷出普通物品」的漏洞。
    /// </summary>
    public class BlackBox: Interactable
    {
        // 初始隐藏内部道具，扫描完成后黑盒删除，生成内部道具
        [Tooltip("扫描所需时间（基准值）。抽到稀有物品时会再乘稀有倍率")]
        public float scanReqTime = 1.0f;

        [Tooltip("黑盒内部道具。配了就固定出这个，不随机、扫描时间也不翻倍")]
        public PickableItem hidedItem;

        [Tooltip("随机出货源：商品池资产。留空则开不出东西")]
        public ItemPool itemPool;

        [Header("随机出货")]
        [Tooltip("开出稀有物品的基准概率。0.1 = 10%")]
        [Range(0f, 1f)]
        public float rareChance = 0.1f;

        [Tooltip("抽到稀有物品时扫描时间的倍率。2 = 翻倍")]
        [Min(1f)]
        public float rareScanTimeMultiplier = 2f;

        [Tooltip("扫描进度UI（整体）")]
        public Image scanUI;
        
        [Tooltip("扫描进度UI（process bar）")]
        public Image scanUIProcess;
        
        [Tooltip("已经扫描了多少")]
        public float scannedTime;

        /// <summary>本次要吐出来的模板。在 <see cref="RollLoot"/> 里定下来</summary>
        private PickableItem _pendingItem;

        /// <summary>本次实际需要的扫描时间（稀有物品会翻倍）。&lt;= 0 表示还没定过</summary>
        private float _scanDuration;

        /// <summary>这次已经掷过骰了吗。一次扫描周期只掷一次，中途取消不重掷</summary>
        private bool _rolled;

        /// <summary>
        /// 本次扫描时长。正常情况就是 <see cref="_scanDuration"/>；
        /// 兜底处理「OnEnable 没跑过」的情况（例如运行时才 AddComponent），避免除以 0。
        /// </summary>
        private float ScanDuration => _scanDuration > 0f ? _scanDuration : Mathf.Max(0.01f, scanReqTime);

        public float ScannedRate => Mathf.Clamp01(scannedTime / ScanDuration);

        /// <summary>
        /// 这次出货的实际稀有概率 = 基准 × (1 + 幸运)。
        /// 幸运是 <see cref="BuffKey.LuckFlat"/> 的加法总和（0.6 = +60%）。
        /// </summary>
        public float RareChance
        {
            get
            {
                float luck = 0f;
                PlayerBuffs buffs = PlayerBuffs.Current;
                if (buffs != null)
                {
                    luck = buffs.GetFlat(BuffKey.LuckFlat);
                }

                return LootRoll.RareChance(rareChance, luck);
            }
        }

        public override string InteractionPrompt => "按E扫描";

        public override bool CanInteract(PlayerBasic player)
        {
            return GameManager.IsNight && base.CanInteract(player);
        }

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            ;
        }

        protected void OnEnable()
        {
            if (scanUI != null)
            {
                scanUI.gameObject.SetActive(false);
            }

            scannedTime = 0;

            // 重新启用（对象池复用 / 开关 GameObject）时允许重掷
            _rolled = false;
            _pendingItem = null;
            _scanDuration = 0f;
        }

        /// <summary>
        /// 第一次真正开始扫描时才掷骰，而不是在 OnEnable。
        ///
        /// 原因是加载顺序：Unity 不保证一个物体的 OnEnable 晚于另一个物体的 Awake，
        /// 黑盒的 OnEnable 有可能跑在 GameManager.Awake / PlayerBuffs.Start 之前，
        /// 那时候 <see cref="PlayerBuffs.Current"/> 还是 null，幸运值会被静默漏掉。
        /// 挪到玩家已经开始扫描的这一帧，玩家和 Buff 一定都在场。
        /// </summary>
        private void RollLootOnce()
        {
            if (_rolled)
            {
                return;
            }

            _rolled = true;
            RollLoot();
        }

        /// <summary>
        /// 决定这一盒出什么，以及要扫多久。每次扫描周期只调一次（见 <see cref="RollLootOnce"/>）。
        /// </summary>
        private void RollLoot()
        {
            _scanDuration = Mathf.Max(0.01f, scanReqTime);

            // 配了固定物品：照旧，不随机也不翻倍
            _pendingItem = hidedItem;
            if (_pendingItem != null)
            {
                return;
            }

            if (itemPool == null)
            {
                Debug.LogWarning($"[{name}]：没连商品池资产（ItemPool），黑盒开不出任何东西", this);
                return;
            }

            bool wantRare = UnityEngine.Random.value < RareChance;
            PickableItem template = null;

            // 先抽目标档位；那一档是空的（例如还没人配稀有物品）就退到另一档，
            // 否则「抽中了稀有空档」这盒就白开了。
            if (!itemPool.TryPickRandom(wantRare, out template) &&
                !itemPool.TryPickRandom(!wantRare, out template))
            {
                Debug.LogWarning($"[{name}]：商品池里一件东西都没有，黑盒开不出任何东西", this);
                return;
            }

            _pendingItem = template;

            // 按「实际抽到的物品」判断稀有度，而不是按「想抽的档位」——
            // 上面有退档逻辑，两者可能不一致。
            if (LootRoll.IsRare(template.SpecialLevel))
            {
                _scanDuration *= rareScanTimeMultiplier;
            }
        }

        private void Update()
        {
            Sfx.Emitter(this).SetPlaying(SoundId.Search, InteractState == InteractState.Interacting, SoundBus.World, true);
            if (InteractState == InteractState.Interacting)
            {
                // 掷骰要在读 ScanDuration 之前，否则第一帧的进度条会用错时长
                RollLootOnce();

                if (!scanUI.IsActive()) scanUI.gameObject.SetActive(true);
                
                scanUIProcess.transform.localScale = new Vector3(ScannedRate, 1, 1);
                
                scannedTime += Time.deltaTime;
                if (scannedTime >= ScanDuration)
                {
                    Sfx.Emitter(this).StopAll();
                    Sfx.Play(SoundId.Achievement, this);
                    SpawnPendingItem();
                    Destroy(gameObject);
                }
            }
            else
            {
                if (scanUI.IsActive())
                {
                    scanUI.gameObject.SetActive(false);
                    scannedTime = 0;
                }
            }
        }

        /// <summary>吐出货。抽的时候没定下来到东西（池子空 / 没连 MouseStore）就不吐，别抛空引用。</summary>
        private void SpawnPendingItem()
        {
            if (_pendingItem == null)
            {
                return;
            }

            PickableItem spawned = Instantiate(_pendingItem, transform.position, Quaternion.identity);

            // 和夜晚生成 / 商店刷新一致：食物营养乘日期权重（1.13^天数）。
            // 黑盒是夜里开的，吐出来的东西不该比别人便宜。
            if (spawned is FoodItem food)
            {
                food.SetSpawnNutritionMultiplier(DayScaling.NutritionWeight);
            }
        }
    }
}
