using TapTapGameJam.Audio;
using System.Collections.Generic;
using TapTapGameJam.Buffs;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Items;
using TapTapGameJam.Tutorial;
using UnityEngine;
using TapTapGameJam.Core;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TapTapGameJam.UI;

namespace TapTapGameJam.Cockroach
{

    /// <summary>
    /// 蟑螂巢穴。既是归巢目标，也是信息素归巢通道的起点。
    /// </summary>
    public partial class CockNest : Interactable
    {
        [SerializeField, HideInInspector] private string _saveId;
        private string _runtimeSaveId;
        public string SaveId => _runtimeSaveId ??= BuildSaveId();

        private void Awake() { _runtimeSaveId = BuildSaveId(); }
        private string BuildSaveId()
        {
            if (!string.IsNullOrEmpty(_saveId)) return _saveId;
            // Capture the authored hierarchy before carrying/reparenting changes it.
            string path = "";
            for (Transform node = transform; node != null; node = node.parent)
                path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
            return gameObject.scene.path + path;
        }

        public NestSaveEntry CaptureState() => new NestSaveEntry {
            id = SaveId, position = transform.position, rotation = transform.rotation,
            state = nestLockState, population = InsideCount,
            spawnProgress = _spawnSpentTime, firstHatchProgress = _firstHatchNutrition,
            hatchNutrition = _hatchNutrition, command = currentCommand,
            firstHatchReserve = _firstHatchReserve
        };

        public void RestoreState(NestSaveEntry data)
        {
            foreach (var roach in new List<Cockroach>(CockroachList))
                if (roach != null) { roach.gameObject.SetActive(false); Destroy(roach.gameObject); }
            CockroachList.Clear();
            InsideCount = 0;
            transform.SetPositionAndRotation(data.position, data.rotation);
            nestLockState = data.state;
            _spawnSpentTime = data.spawnProgress;
            _firstHatchNutrition = data.firstHatchProgress;
            _firstHatchReserve = Mathf.Clamp(data.firstHatchReserve, 0f, _firstHatchNutritionCost);
            _hatchNutrition = data.hatchNutrition;
            currentCommand = data.command;
            if (data.population > 0 && _cockroachPrefab == null)
                throw new System.InvalidOperationException($"巢穴 {name} 缺少蟑螂预制体，无法恢复数量。");
            for (int i = 0; i < data.population; i++) SpawnCockroach();
            DispatchCommand(currentCommand);
            EnsureRing();
            RefreshDistanceField();
        }

        public enum NestLockState
        {
            /// <summary>
            /// 还没被玩家认领。只有白天能交互：点一次就标记成 <see cref="Locked"/>，且此状态下不能被搬走。
            /// </summary>
            UnFounded,
            Locked,
            FirstHatching,
            Unlocked
        }

        [Tooltip("巢穴解锁状态。新巢穴默认 UnFounded：白天标记一次后才会进入 Locked")] 
        public NestLockState nestLockState = NestLockState.UnFounded;

        [Tooltip("产卵（解锁）所需时间")] [SerializeField]
        private float _spawnTime = 5.0f;
        private float _spawnSpentTime = 0.0f;

        [Header("初次孵化：消耗营养（产卵阶段不消耗）")]
        [Tooltip("孵出第一批总共要消耗的营养")]
        [SerializeField, Min(0f)]
        private float _firstHatchNutritionCost = 60f;

        [Tooltip("孵化时每秒消耗多少营养。池子里的不够就只扣现有的，进度跟着停住")]
        [SerializeField, Min(0f)]
        private float _firstHatchNutritionPerSecond = 3f;

        [Tooltip("玩家在旁边交互时，孵化效率（每秒消耗）的倍率。只加快进度，总消耗量不变")]
        [SerializeField, Min(1f)]
        private float _firstHatchInteractBoost = 5f;

        /// <summary>首次孵化已经吃掉多少营养。进度 = 它 / <see cref="_firstHatchNutritionCost"/></summary>
        private float _firstHatchNutrition;

        /// <summary>
        /// 首次孵化专用的<b>小营养池</b>：从共享池预支过来的、以及吸到的食物营养暂存在这里，
        /// 上限 = 首次孵化还差多少（<c>Cost - _firstHatchNutrition</c>），**不会超过需求**。
        ///
        /// 它只负责"占住"营养：孵化进度仍然按时间从这个小池子里慢慢消耗
        /// （见 <see cref="UpdateFirstHatch"/>）。预支 / 吸食物本身都不推进进度。
        /// </summary>
        private float _firstHatchReserve;

        /// <summary>上一次结算时池子里有没有营养。只用来让"没营养了"这条日志每个枯竭期只打一次</summary>
        private bool _firstHatchStarved;

        [Tooltip("初次孵化产生的蟑螂数量")] [SerializeField]
        private int _firstHatchCount = 3;

        public bool isCockroachActive => nestLockState == NestLockState.Unlocked && !GameManager.IsDay;


        // =========== 夜间巢穴属性 ============== //

        /// <summary>巢穴距离场里"不可达"的哨兵值</summary>
        public const int FarDistance = GridDistanceField.Unreachable;

        /// <summary>孵化指令的颜色。巢穴自己的 UI 文本和范围圈都用它，改这里两边一起变</summary>
        public static readonly Color HatchColor = new Color(0.95f, 0.8f, 0.35f, 1f);

        /// <summary>觅食指令的颜色。巢穴自己的 UI 文本和范围圈都用它</summary>
        public static readonly Color ForagingColor = new Color(0.35f, 0.85f, 0.9f, 1f);

        public Vector2 WorldPosition => Cockroach.FlatOf(transform.position);

        [Tooltip("巢穴本体半径（世界单位）。蟑螂走到这个范围内就算到家")]
        public float radius = 0.6f;

        [Tooltip("活动范围半径（世界单位）。超出所有巢穴范围的蟑螂会被拉回来")]
        public float range = 10f;

        [Header("巢穴吸食周围食物")]
        [Tooltip("每隔多少帧扫描一次巢穴周围的食物")]
        [SerializeField, Min(1)]
        private int _absorbScanFrames = 10;

        [Tooltip("吸食半径（世界单位）：这个范围内的食物会被整个吸掉，不用蟑螂去搬")]
        [SerializeField, Min(0.1f)]
        private float _absorbRadius = 1.5f;

        [Tooltip("一次扫描最多吸几件，避免范围开很大时一帧啃穿一地")]
        [SerializeField, Min(1)]
        private int _absorbMaxPerScan = 8;

        /// <summary>吸食扫描的帧计数</summary>
        private int _absorbFrameCounter;

        /// <summary>上次读到 Buff 时的 Revision，用来判断 <see cref="EffectiveRange"/> 要不要重算</summary>
        private int _rangeRevision = int.MinValue;
        private float _rangeFactor = 1f;

        /// <summary>
        /// 实际生效的活动范围 = <see cref="range"/> × 「巢穴探索范围」Buff 系数。
        ///
        /// <b>基准值仍然是 <see cref="range"/> 这个序列化字段</b>（别改名：改名等于改存档），
        /// Buff 只乘在读取点上，绝不写回去——写回就变成不可回滚的全局改写。
        ///
        /// 每次读只多一次 Revision 比较，所以蟑螂每帧读也没问题。
        /// 注意：<b>只放大 range，不放大 radius</b>——radius 是巢穴本体，放大它会让巢口交互距离、
        /// 归巢判定、落巢散布一起变，那不是「探索范围」该管的事。
        /// </summary>
        public float EffectiveRange
        {
            get
            {
                PlayerBuffs buffs = PlayerBuffs.Current;
                int revision = buffs != null ? buffs.Revision : -1;
                if (revision != _rangeRevision)
                {
                    _rangeFactor = buffs != null ? buffs.CockroachSnapshot.NestRangeFactor : 1f;
                    _rangeRevision = revision;
                }

                return range * _rangeFactor;
            }
        }

        [Header("范围虚线圈")]
        [Tooltip("范围虚线圈。留空则先从子物体里找，还找不到就在运行时建一个 RangeRing 子物体")]
        [SerializeField] private CockNestRing _ring;

        public List<Cockroach> CockroachList { get; private set; } = new List<Cockroach>();

        [Header("孵化")] [Tooltip("孵出来的蟑螂用它实例化。留空则孵不出东西")] [SerializeField]
        private Cockroach _cockroachPrefab;

        /// <summary>巢内的蟑螂数量。由蟑螂自己上报，不遍历 CockroachList</summary>
        public int InsideCount { get; private set; }

        [Tooltip("一批孵出几只蟑螂")]
        [SerializeField, Min(1)]
        private int _hatchBatchSize = 10;

        [Tooltip("巢穴容量上限：属于这个巢的蟑螂（巢里的 + 在外面跑的）到这个数就停止孵化")]
        [SerializeField, Min(1)]
        private int _populationCap = 80;

        /// <summary>距离孵出下一批还差多少营养。分母是 HatchNutritionCost</summary>
        private float _hatchNutrition;

        private CockroachCommand currentCommand = CockroachCommand.Hatch;

        /// <summary>当前指令。给 UI 读取用</summary>
        public CockroachCommand CurrentCommand => currentCommand;

        /// <summary>
        /// 到本巢穴的格数距离场
        /// </summary>
        public int[,] DistanceField { get; private set; }

        /// <summary>这个巢穴现在一共有多少只蟑螂（巢里的 + 在外面跑的）</summary>
        public int RoachCount => CockroachList.Count;

        /// <summary>巢穴容量上限</summary>
        public int PopulationCap => _populationCap;

        /// <summary>已经养满了，不再孵化</summary>
        public bool IsFull => RoachCount >= _populationCap;

        // 距离孵出下一批的进度 0~1
        public float HatchProgress =>
            Mathf.Clamp01(_hatchNutrition / CockroachManager.Instance.cockroachSetting.HatchNutritionCost);

        // 距离解锁的进度
        public float UnlockProgress => Mathf.Clamp01(_spawnSpentTime / _spawnTime);

        // 初次孵化的进度：按"已经吃掉的营养 / 总需求"算，不再按时间
        public float FirstHatchProgress => _firstHatchNutritionCost > 0f
            ? Mathf.Clamp01(_firstHatchNutrition / _firstHatchNutritionCost)
            : 1f;
        
        private void Update()
        {
            if (GameManager.IsDay) return;

            // 先吸周围的食物，再跑孵化：这样这一帧吸到的营养当帧就能算进首次孵化
            AbsorbNearbyFood();

            if (nestLockState == NestLockState.FirstHatching)
            {
                UpdateFirstHatch(Time.deltaTime);
            }
            else if (nestLockState == NestLockState.Locked)
            {
                if (InteractState == InteractState.Interacting)
                {
                    _spawnSpentTime += Time.deltaTime;
                    if (_spawnSpentTime >= _spawnTime)
                    {
                        // 成功产卵
                        SetLockState(NestLockState.FirstHatching);
                        TutorialSignal.Send(TutorialSignals.NestFirstEgg);
                        // 打断一下，要重新点才能继续孵化
                        InteractState = InteractState.InteractPending;
                    }
                }
                else
                {
                    _spawnSpentTime = 0;
                }
            } else if (nestLockState == NestLockState.Unlocked)
            {
                UpdateNight();
            }
        }

        /// <summary>
        /// 首次孵化：进度按"每秒消耗营养"推进，总需求 <see cref="_firstHatchNutritionCost"/> 不变。
        ///
        /// 每帧两步：
        /// ① <b>预支</b>：把共享池里的营养提前抢进巢穴的小池子 <see cref="_firstHatchReserve"/>，
        ///    上限是"还差多少"。池子里有多少抢多少，抢不到就下一帧再说。
        ///    这一步只是"占住"，进度一点都不涨。
        /// ② <b>消耗</b>：从小池子里按 <see cref="_firstHatchNutritionPerSecond"/> 慢慢扣，
        ///    扣掉多少进度才涨多少。小池子空了就停在这里等（池子不会被扣成负数）。
        ///
        /// 【玩家加速】只乘在"每秒消耗速率"上：总需求不变，
        /// 加速只是让它更快吃满、更快孵出来，吃掉的营养总量一模一样。
        /// </summary>
        private void UpdateFirstHatch(float dt)
        {
            float remaining = _firstHatchNutritionCost - _firstHatchNutrition;
            if (remaining <= 0f)
            {
                FinishFirstHatch();
                return;
            }

            CockroachManager manager = CockroachManager.Instance;
            if (manager == null)
            {
                return;
            }

            bool boosting = InteractState == InteractState.Interacting;
            float rate = _firstHatchNutritionPerSecond * (boosting ? _firstHatchInteractBoost : 1f);

            // 只要还差的那部分，避免最后一帧多吃。
            // 暂停时 dt = 0，这里直接返回 —— 下面的预支也不会搬营养。
            float wanted = Mathf.Min(rate * dt, remaining);
            if (wanted <= 0f)
            {
                return;
            }

            // ① 预支：先把共享池里还差的那部分抢到自己的小池子里（池子有多少抢多少，不多占）
            PreemptSharedNutrition(manager, remaining);

            // ② 消耗：小池子里的营养按时间慢慢花，花掉多少进度才涨多少

            float spent = Mathf.Min(wanted, _firstHatchReserve);
            if (spent <= 0f)
            {
                if (!_firstHatchStarved)
                {
                    _firstHatchStarved = true;
                    Debug.Log($"[CockNest:{name}] 小营养池和共享池都是空的，首次孵化暂停（还差 {remaining:0.#} 营养）", this);
                }

                return;
            }

            _firstHatchStarved = false;
            _firstHatchReserve -= spent;
            _firstHatchNutrition += spent;

            if (_firstHatchNutrition >= _firstHatchNutritionCost)
            {
                FinishFirstHatch();
            }
        }

        /// <summary>
        /// 预支：把共享池里的营养提前抢进巢穴的小池子，上限是"首次孵化还差多少"。
        ///
        /// 这就是"抢占"的全部含义 —— <b>只是把营养挪到自己这里占住</b>，
        /// 别的巢 / 批次孵化就花不到了；孵化进度一点都不涨，还是要按时间慢慢消耗。
        /// 共享池里有多少抢多少（部分扣款），抢不到就等下一帧。
        /// </summary>
        private void PreemptSharedNutrition(CockroachManager manager, float remaining)
        {
            float room = remaining - _firstHatchReserve;
            if (room <= 0f)
            {
                return;
            }

            float taken = manager.SpendSharedNutrition(room);
            if (taken > 0f)
            {
                _firstHatchReserve += taken;
            }
        }

        /// <summary>
        /// 每 <see cref="_absorbScanFrames"/> 帧扫一次巢穴周围：半径内的食物直接整个吸掉，不用蟑螂搬。
        ///
        /// 为什么是"吸"而不是等蟑螂运：首次孵化卡住时最难受的就是干等，
        /// 巢穴自己把脚边的食物吃掉，才能让"围着巢堆食物 → 立刻孵化"这个玩法成立。
        ///
        /// 只跳过 <see cref="NestLockState.UnFounded"/>（还没被标记的点还不是巢）和被玩家搬走的巢。
        /// 营养去向见 <see cref="DepositAbsorbed"/>。
        /// </summary>
        private void AbsorbNearbyFood()
        {
            // 暂停时不吸（背包 / 商店 / 仓库 / ESC / 教程 都是 timeScale = 0）。
            // 这里的扫描是按帧数走的，timeScale = 0 拦不住它 —— 不显式拦的话，
            // 玩家在背包里拖东西、或者开着面板发呆时，地上的食物会被巢穴悄悄吸走。
            // 放在帧计数之前：暂停期间连计数都不推进，恢复后仍是"每 10 帧一次"。
            if (GamePauseManager.Instance.IsPaused)
            {
                return;
            }

            // 只有"已经在孵化"或"已经解锁"的巢才吸：UnFounded 还不算巢，
            // Locked（产卵阶段）也不吸 —— 那时候巢还没开工。
            bool canAbsorb = nestLockState == NestLockState.FirstHatching || nestLockState == NestLockState.Unlocked;
            if (!canAbsorb || IsCarried)
            {
                return;
            }

            if (++_absorbFrameCounter < _absorbScanFrames)
            {
                return;
            }

            _absorbFrameCounter = 0;

            FoodManager foods = FoodManager.Instance;
            if (foods == null || _absorbRadius <= 0f)
            {
                return;
            }

            Vector2 center = WorldPosition;
            for (int i = 0; i < _absorbMaxPerScan; i++)
            {
                if (!foods.TryFindNearestFood(center, _absorbRadius, out FoodItem food, out Vector2 eatPoint))
                {
                    return;
                }

                // TryFindNearestFood 是按"格"找的，最外圈可能略微超出半径；
                // 最近的那件都在范围外，就说明周围已经没东西了
                if (Vector2.Distance(center, eatPoint) > _absorbRadius)
                {
                    return;
                }

                float taken = food.Consume(food.remainingNutrition);
                if (taken <= 0f)
                {
                    // 已经被别人啃空（销毁是帧末生效的），跳过继续找下一件
                    continue;
                }

                DepositAbsorbed(taken);
            }
        }

        /// <summary>
        /// 吸到的营养按优先级分配：<b>先补首次孵化，够了剩下的才进共享营养池</b>。
        /// 首次孵化不在进行中（还没产卵 / 已经孵完）就整份进池子。
        /// </summary>
        private void DepositAbsorbed(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            // 首次孵化期间：先塞进小营养池（上限 = 还差多少），剩下的才进共享池。
            // 注意这里**只搬营养、不推进进度** —— 进度依然由 UpdateFirstHatch 按时间消耗。
            if (nestLockState == NestLockState.FirstHatching)
            {
                float need = _firstHatchNutritionCost - _firstHatchNutrition;
                float room = Mathf.Max(0f, need - _firstHatchReserve);
                if (room > 0f)
                {
                    float toReserve = Mathf.Min(amount, room);
                    _firstHatchReserve += toReserve;
                    amount -= toReserve;
                    _firstHatchStarved = false;
                }
            }

            if (amount <= 0f)
            {
                return;
            }

            CockroachManager manager = CockroachManager.Instance;
            if (manager != null)
            {
                manager.AddSharedNutrition(amount);
            }
        }

        /// <summary>首次孵化完成：吐出第一批蟑螂并进入 Unlocked</summary>
        private void FinishFirstHatch()
        {
            for (int i = 0; i < _firstHatchCount; i++)
            {
                SpawnCockroach();
            }

            SetLockState(NestLockState.Unlocked);
            TutorialSignal.Send(TutorialSignals.NestFirstHatch);
        }


        void UpdateNight()
        {
            // 夜间蟑螂逻辑
            UpdateHatch(Time.deltaTime);

            // 指令变了就换颜色。放在这里是为了不放过任何改 currentCommand 的路径。
            // 自转不在这里做，由 CockNestRing 自己按当前指令跑。
            _ring?.SetCommand(currentCommand);
        }

        /// <summary>
        /// 拿到范围圈：先用 Inspector 里挂的引用，其次子物体，最后运行时建一个专用子物体。
        /// 网格、材质、自转都在 CockNestRing 里，这里只把当前半径交给它。
        /// </summary>
        private void EnsureRing()
        {
            if (_ring == null) _ring = GetComponentInChildren<CockNestRing>(true);

            if (_ring == null)
            {
                var ringObject = new GameObject("RangeRing");
                ringObject.transform.SetParent(transform, false);
                _ring = ringObject.AddComponent<CockNestRing>();
            }

            _ring.Regenerate(EffectiveRange);
            RefreshRingVisibility();
        }

        /// <summary>
        /// 范围圈是否该显示：夜间、已标记、没被搬着，且已经过了"产卵"阶段（那会儿还没有活动范围可言）。
        ///
        /// <b>巢穴的命令按钮也用这一条判断</b>（见 <c>UI/NestCommandButton</c>），
        /// 所以圈和按钮永远同进同出 —— 要改显示时机只改这里一处。
        /// </summary>
        public bool IsRangeRingVisible =>
            GameManager.IsNight
            && !IsCarried
            && nestLockState != NestLockState.UnFounded
            && nestLockState != NestLockState.Locked;

        /// <summary>
        /// 范围圈只在夜间、已标记（UnFounded 不算）、且没被玩家搬着的时候显示。
        /// 未标记的巢穴玩家还没"发现"，连范围都不该露出来。
        /// </summary>
        private void RefreshRingVisibility()
        {
            bool visible = IsRangeRingVisible;
            if (_ring == null || _ring.IsVisible == visible) return;

            if (visible)
            {
                // 显示出来的这一刻保证颜色跟当前指令一致
                _ring.SetCommand(currentCommand);
            }

            _ring.SetVisible(visible);
        }

        /// <summary>切换解锁状态。内部的迁移都走这里，保证范围圈的显示跟着更新</summary>
        private void SetLockState(NestLockState state)
        {
            if (nestLockState == state) return;

            nestLockState = state;
            Sfx.Play(SoundId.Achievement, this);
            RefreshRingVisibility();
        }

        /// <summary>
        /// 玩家认领一个还没标记的巢穴：UnFounded → Locked。
        /// 白天、夜间都能标记（夜间能不能点到由 <see cref="CanInteract"/> 的距离限制决定）；
        /// 标记之后白天允许搬运，夜里则要继续长按产卵，孵化出第一批才会进入 Unlocked。
        /// 返回是否真的完成了标记：已经标记过 / 正被搬着 / 购物阶段，都返回 false。
        /// </summary>
        public bool TryFoundNest()
        {
            if (nestLockState != NestLockState.UnFounded) return false;
            if (IsCarried) return false;

            // 只有白天和夜里两个阶段能标记；购物阶段两边都是 false，不给操作
            if (!GameManager.IsDay && !GameManager.IsNight) return false;

            SetLockState(NestLockState.Locked);
            TutorialSignal.Send(TutorialSignals.NestFirstMarked);

            // 这是一次性交互，做完要把状态打回 Pending：
            // 否则 Interacting 会一直留到夜里，巢穴会以为玩家一直按着 E，不用按就自己攒产卵进度
            InteractState = InteractState.InteractPending;
            return true;
        }

        private void Start()
        {
            EnsureRing();
            RefreshDistanceField();
            
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            // 和 PickableItem 一样由自己登记，CockroachManager 不主动遍历场景
            CockroachManager.Instance.CockNestList.Add(this);
            Debug.Log("Nest created.");
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<BuffsRebuiltEvent>(HandleBuffsRebuilt);
        }

        /// <summary>
        /// 巢穴探索范围是 Buff 乘出来的，Buff 一变虚线圈就得跟着改，
        /// 否则玩家看到的圈和蟑螂真实的活动范围对不上。
        /// <see cref="CockNestRing.Regenerate"/> 半径没变会自己跳过，所以这里可以放心每次调。
        /// </summary>
        private void HandleBuffsRebuilt(BuffsRebuiltEvent evt)
        {
            if (_ring == null)
            {
                // Start 还没跑过：EnsureRing / HandlePhaseChanged 会按新半径建好
                return;
            }

            _ring.Regenerate(EffectiveRange);
            RefreshRingVisibility();
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<BuffsRebuiltEvent>(HandleBuffsRebuilt);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            UnregisterInteractionTargets();
            if(CockroachManager.Instance)
                CockroachManager.Instance.CockNestList.Remove(this);

            CockroachList.Clear();

            // 拆场景时蟑螂的 OnDisable 不保证会跑，计数和进度直接归零，别留给下次启用
            InsideCount = 0;
            _hatchNutrition = 0f;

            // TODO: 移除List中所有蟑螂（删除实例）
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            bool enteringNight = evt.NewPhase == GamePhase.Night;

            // 每次进入夜晚重建一次范围圈：夜间才看得见它，半径这时候可能已经变过
            if (enteringNight)
            {
                _ring.Regenerate(EffectiveRange);
                RefreshDistanceField();
            }
            // 天亮后怎么处理没回巢的蟑螂，由 NightManager 按"清场"开关统一决定
            // （勾了 = 击杀，没勾 = 全部瞬移回巢）。这里不再自己杀：
            // 否则开关关掉时会被这一刀抢先杀掉，那个开关就形同虚设。

            RefreshRingVisibility();
        }

        // 不含生成/销毁语义，仅记录 & 注册一只蟑螂
        public void Add(Cockroach cockroach)
        {
            if (cockroach == null || CockroachList.Contains(cockroach)) return;
            CockroachList.Add(cockroach);
            if (cockroach.IsInside) InsideCount++;
        }

        public void Remove(Cockroach cockroach)
        {
            if (CockroachList.Remove(cockroach) && cockroach.IsInside) InsideCount--;
        }

        /// <summary>蟑螂进巢时自己来报。外部不要直接改 InsideCount</summary>
        public void ReportWentInside()
        {
            InsideCount++;
        }

        /// <summary>蟑螂离巢时自己来报</summary>
        public void ReportWentOutside()
        {
            InsideCount--;
        }

        /// <summary>
        /// 孵化：按**批次**生产。攒够一批的营养就一次孵 <see cref="_hatchBatchSize"/> 只，
        /// 到 <see cref="_populationCap"/> 就停产。
        ///
        /// 速率是**固定值**（<c>HatchNutritionPerSecond</c>），和巢内虫数无关 ——
        /// 以前是"每只虫每秒吃一份"，虫越多越快；现在一批固定 30 营养 / 20 秒。
        /// 只在 Hatch 指令下进行；中途切走会保留进度。
        /// </summary>
        private void UpdateHatch(float dt)
        {
            if (currentCommand != CockroachCommand.Hatch || _cockroachPrefab == null)
            {
                return;
            }

            // 养满了就停产（进度保留，等虫数掉下来再接着攒）
            if (IsFull)
            {
                return;
            }

            CockroachManager manager = CockroachManager.Instance;
            CockroachSettings settings = manager.cockroachSetting;

            // 想吃的量和真正吃到的量分开：营养池不够时进度只按实际消耗涨
            // 产卵速度加成乘在「每秒消耗速率」上，不动 HatchNutritionCost：
            // 进度条的分母是 Cost，动它 UI 就得跟着改，而乘速率进度条自动就对。
            float wanted = settings.HatchNutritionPerSecond * dt * manager.BuffMods.HatchFactor;
            float spent = Mathf.Min(wanted, settings.HatchNutritionCost - _hatchNutrition);

            if (spent <= 0f || !manager.TrySpendSharedNutrition(spent))
            {
                return;
            }

            _hatchNutrition += spent;

            if (_hatchNutrition < settings.HatchNutritionCost)
            {
                return;
            }

            _hatchNutrition -= settings.HatchNutritionCost;

            // 一批 N 只；离容量上限不到 N 只就只补到上限
            int count = Mathf.Min(_hatchBatchSize, _populationCap - RoachCount);
            for (int i = 0; i < count; i++)
            {
                SpawnCockroach();
            }
        }

        /// <summary>孵一只新蟑螂，落在巢内并进入 Inside 待命</summary>
        private void SpawnCockroach()
        {
            if (_cockroachPrefab == null)
            {
                return;
            }

            Cockroach cockroach = Instantiate(_cockroachPrefab, transform.position, Quaternion.identity);

            cockroach.Position = WorldPosition;

            cockroach.SpawnInside(this);
        }
        
        /// <summary>
        /// 点击巢穴时调用的入口：切到下一条行动指令，然后派发。
        /// </summary>
        public void CycleCommand()
        {
            if (!isCockroachActive) return;
            int count = System.Enum.GetValues(typeof(CockroachCommand)).Length;
            currentCommand = (CockroachCommand)(((int)currentCommand + 1) % count);

            DispatchCommand(currentCommand);
            TutorialSignal.Send(TutorialSignals.NestFirstCommand);
            Debug.Log("Nest command: " + currentCommand);

            // 播报：整巢指令切换（点巢穴按钮）才播，不是每只蟑螂出门/回家都播
            BroadcastUI.Broadcast(currentCommand == CockroachCommand.Foraging
                ? BroadcastKind.LeaveNest
                : BroadcastKind.ReturnNest);
        }

        // 给所属所有蟑螂派发行动指令
        public void DispatchCommand(CockroachCommand command)
        {
            foreach (Cockroach cockroach in CockroachList)
            {
                cockroach.nestCommand = command;
            }
        }

        /// <summary>
        /// 更新"绕过障碍到本巢穴"的格数距离场。
        /// 算法在 <see cref="GridDistanceField"/> 里，和守卫的"回出生点"共用一份实现。
        /// </summary>
        public void RefreshDistanceField()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || grid.MapState == null)
            {
                return;
            }

            DistanceField = GridDistanceField.Build(grid, grid.WorldToGrid(WorldPosition));
        }

        /// <summary>
        /// 某世界坐标（XZ 平面）到本巢穴的格数距离；不可达返回 />
        /// </summary>
        public int NestDistance(Vector2 worldPos)
        {
            GridManager grid = GridManager.Instance;
            Vector2Int cell = grid.WorldToGrid(worldPos);
            return DistanceField[cell.x, cell.y];
        }
    }
}
