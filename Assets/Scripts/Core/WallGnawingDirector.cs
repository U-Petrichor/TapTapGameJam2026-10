using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Buffs;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 「群颚破壁」（<c>group_wall_gnawing</c>）的落地：拿到 tag 的那个夜晚，
    /// 沿着网格逐格打一遍射线，把扫到的所有 <b>Wall / Desk</b> 层物体注入 <see cref="WallFoodItem"/>，
    /// 让墙和桌子变成能吃的东西。
    ///
    /// <b>只监听 <see cref="GamePhaseChangedEvent"/>，全场只注入一次。</b>
    /// 每个夜里进来只看一眼：带着 tag 就扫一次并永久记下；还没解锁就什么都不做，
    /// 下一夜再看（天赋可能在白天的购物阶段才点出来，一进来就"记账失败"会让它永远不生效）。
    ///
    /// 为什么把扫描本身放在这里而不是每次吃墙时做：这是**一次性**的开销。
    /// 格子是 500×300 = 15 万格，逐格射线摊到多帧执行（默认 3000 条/帧 ≈ 0.8 秒），
    /// 且只发生在夜里入场的时候，玩家看不到卡顿。
    ///
    /// 挂载位置：跟 <see cref="GridManager"/> 同一个物体（见 Assets/Prefabs/Grid.prefab），
    /// 引用都摆在 Inspector 上，方便调数值和换材质。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallGnawingDirector : MonoBehaviour
    {
        /// <summary>Wall 层号（ProjectSettings/TagManager.asset）</summary>
        public const int WallLayer = 6;

        /// <summary>Desk 层号（桌子 Table1~8 都在这一层）</summary>
        public const int DeskLayer = 10;

        /// <summary>默认扫描的层：Wall + Desk</summary>
        public const int WallAndDeskMask = (1 << WallLayer) | (1 << DeskLayer);

        [Header("引用")]
        [Tooltip("网格。留空则先从自己 / 父物体上找，再退到 GridManager.Instance")]
        [SerializeField] private GridManager _grid;

        [Tooltip("要变成食物的物理层。默认 Wall(6) + Desk(10)")]
        [SerializeField] private LayerMask _targetLayers = WallAndDeskMask;

        [Tooltip("裂纹覆盖材质（Assets/Material/WallCrack.mat）。留空则墙体被啃了没有任何视觉反馈")]
        [SerializeField] private Material _crackMaterial;

        [Header("营养量（按包围盒体积估算）")]
        [Tooltip("每立方米给多少营养。墙体包围盒约 1.2m³、桌子约 3m³")]
        [SerializeField, Min(0.01f)] private float _nutritionPerCubicMeter = 25f;

        [Tooltip("单件营养下限，防止小装饰件变成一口就没")]
        [SerializeField, Min(0.01f)] private float _minNutrition = 8f;

        [Tooltip("单件营养上限，防止一整面承重墙变成无限营养池")]
        [SerializeField, Min(0.01f)] private float _maxNutrition = 120f;

        [Header("扫描")]
        [Tooltip("每帧最多打多少条射线。15 万格 ÷ 3000 ≈ 50 帧 ≈ 0.8 秒")]
        [SerializeField, Min(100)] private int _raysPerFrame = 3000;

        [Tooltip("关闭则只在 Console 里报错，不打扫描进度和汇总")]
        [SerializeField] private bool _logSummary = true;

        [Tooltip("巡检：每 0.5 秒检查一遍已注入的墙体，营养归零却没销毁的补一次销毁")]
        [SerializeField] private bool _watchDepleted = true;

        /// <summary>扫描结果，复用同一份 List</summary>
        private readonly List<GameObject> _targets = new List<GameObject>();

        /// <summary>已经在场上的可食用墙体。巡检销毁、统计都用它，不用每帧 FindObjects</summary>
        private readonly List<WallFoodItem> _injected = new List<WallFoodItem>();

        /// <summary>已经扫过并注入过了。全场只置位一次</summary>
        private bool _injectedOnce;

        private Coroutine _scan;
        private Coroutine _watch;

        #region 生命周期

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            // 每夜入场看一眼就够了：进夜晚的那一下是黑屏过渡，正好适合干重活
            if (evt.NewPhase != GamePhase.Night)
            {
                return;
            }

            TryInjectOnNight();
        }

        #endregion

        #region 注入

        private void TryInjectOnNight()
        {
            if (_injectedOnce)
            {
                return;
            }

            if (!HasWallGnawingTag())
            {
                // 还没解锁：什么都不做，下一夜再看。不要在这里置位，
                // 否则白天的购物阶段才点出天赋的玩家永远等不到它生效
                return;
            }

            GridManager grid = ResolveGrid();
            if (grid == null)
            {
                Debug.LogError("[WallGnawingDirector]：场景里找不到 GridManager，「群颚破壁」无法扫描墙体。", this);
                return;
            }

            _injectedOnce = true;
            _scan = StartCoroutine(ScanAndInject(grid));
        }

        /// <summary>
        /// 逐格射线 → 收集 Wall/Desk 层物体 → 逐个注入 <see cref="WallFoodItem"/>。
        /// 扫描本身在 <see cref="GridManager"/> 里（它才拥有坐标约定），这里只管怎么用结果。
        /// </summary>
        private IEnumerator ScanAndInject(GridManager grid)
        {
            float startTime = Time.realtimeSinceStartup;

            if (_logSummary)
            {
                Debug.Log(
                    $"[WallGnawingDirector]：开始逐格射线扫描 {grid.width}×{grid.height} 格，" +
                    $"每帧 {_raysPerFrame} 条，目标层 {_targetLayers.value}", this);
            }

            yield return grid.CollectLayerObjectsRoutine(_targetLayers, _targets, _raysPerFrame);

            int injected = 0;
            int skipped = 0;

            for (int i = 0; i < _targets.Count; i++)
            {
                GameObject target = _targets[i];
                if (target == null)
                {
                    continue;
                }

                // 幂等：重复注入没有意义，而且会多挂一次 Awake / 一次注册
                if (target.GetComponent<WallFoodItem>() != null)
                {
                    skipped++;
                    continue;
                }

                float nutrition = EstimateNutrition(target);
                WallFoodItem food = target.AddComponent<WallFoodItem>();
                food.Configure(nutrition, _crackMaterial);
                _injected.Add(food);
                injected++;
            }

            _scan = null;

            StartWatchdog();

            if (_logSummary)
            {
                Debug.Log(
                    $"[WallGnawingDirector]：扫描命中 {_targets.Count} 个物体，注入 {injected} 个，" +
                    $"已有组件跳过 {skipped} 个，耗时 {Time.realtimeSinceStartup - startTime:F2}s", this);
            }
        }

        /// <summary>
        /// 营养量按包围盒体积估：承重墙比一张桌子耐啃，小装饰件也不会变成一口就没了。
        /// 没有碰撞体 / Renderer 的物体按最小量给，至少保证它是"能吃"的。
        /// </summary>
        private float EstimateNutrition(GameObject target)
        {
            Collider collider = target.GetComponentInChildren<Collider>();
            Bounds bounds;

            if (collider != null)
            {
                bounds = collider.bounds;
            }
            else
            {
                Renderer renderer = target.GetComponentInChildren<Renderer>();
                if (renderer == null)
                {
                    return _minNutrition;
                }

                bounds = renderer.bounds;
            }

            float volume = Mathf.Max(0f, bounds.size.x) * Mathf.Max(0f, bounds.size.y) * Mathf.Max(0f, bounds.size.z);
            return Mathf.Clamp(volume * _nutritionPerCubicMeter, _minNutrition, _maxNutrition);
        }

        private static bool HasWallGnawingTag()
        {
            PlayerBuffs buffs = PlayerBuffs.Current;
            return buffs != null && buffs.Has(BuffKey.FlagGroupWallGnawing);
        }

        #region 巡检

        /// <summary>
        /// 每 0.5 秒扫一遍已经注入的墙体，营养归零却没销毁的补一次销毁。
        ///
        /// 这不是主路径：主路径是 <see cref="FoodItem.Consume"/> 啃到 0 的那一帧就安排销毁。
        /// 巡检的作用是「兜底 + 自愈」—— 只要出现营养 0 却还活着的实例，最多半秒内一定被清掉，
        /// 不会出现"墙啃完了还杵在那儿"这种看起来像 bug 的状态。
        /// 顺带把已经被销毁的实例从名单里摘掉，列表只会越来越短。
        /// </summary>
        private void StartWatchdog()
        {
            if (!_watchDepleted || _watch != null || _injected.Count == 0)
            {
                return;
            }

            _watch = StartCoroutine(WatchDepleted());
        }

        private IEnumerator WatchDepleted()
        {
            var wait = new WaitForSeconds(0.5f);

            while (true)
            {
                yield return wait;

                for (int i = _injected.Count - 1; i >= 0; i--)
                {
                    WallFoodItem wall = _injected[i];

                    // 已经销毁的（吃空、或场景卸载）从名单里摘掉
                    if (wall == null)
                    {
                        _injected.RemoveAt(i);
                        continue;
                    }

                    wall.DestroyIfDepleted();
                }
            }
        }

        #endregion

        private GridManager ResolveGrid()
        {
            if (_grid == null)
            {
                _grid = GetComponent<GridManager>();
            }

            if (_grid == null)
            {
                _grid = GetComponentInParent<GridManager>();
            }

            if (_grid == null)
            {
                _grid = GridManager.Instance;
            }

            return _grid;
        }

        #endregion

        #region 排查 / 测试

        /// <summary>
        /// 不用等到夜里、也不看有没有 tag，直接扫一遍。
        /// 调墙体手感 / 裂纹效果时用它，不然每次都要等到入夜 + 点天赋。
        /// </summary>
        [ContextMenu("立即扫描并注入（测试用，忽略 tag 和阶段）")]
        private void DebugInjectNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallGnawingDirector]：扫描要打物理射线、往场景物体上 AddComponent，只能在 Play 模式跑。");
                return;
            }

            if (_scan != null)
            {
                Debug.LogWarning("[WallGnawingDirector]：上一轮扫描还没结束。");
                return;
            }

            GridManager grid = ResolveGrid();
            if (grid == null)
            {
                Debug.LogError("[WallGnawingDirector]：场景里找不到 GridManager。", this);
                return;
            }

            _injectedOnce = true;
            _scan = StartCoroutine(ScanAndInject(grid));
        }

        private void OnValidate()
        {
            if (_maxNutrition < _minNutrition)
            {
                _maxNutrition = _minNutrition;
            }

            if (_raysPerFrame < 100)
            {
                _raysPerFrame = 100;
            }
        }

        /// <summary>
        /// 排查「墙啃完了却还在」：把场上所有被注入的墙 / 桌子统计一遍。
        ///
        /// 重点看 <b>僵尸数</b>：营养已经是 0 却还没被销毁的实例。
        /// 正常情况下它必须是 0；不是 0 就说明销毁路径被打断过（现在是自愈的，下一次被啃会被补掉）。
        /// 还要看 <b>进度分布</b>：如果几百面墙都卡在 0.6~0.9，那不是 bug，是墙太多、
        /// 蟑螂摊得太散，一面墙都喂不饱 —— 调小 <see cref="_nutritionPerCubicMeter"/> 就行。
        /// </summary>
        [ContextMenu("统计已注入的墙体（排查用）")]
        private void DebugReportWalls()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallGnawingDirector]：统计要读运行时状态，进 Play 模式再点。");
                return;
            }

            WallFoodItem[] walls = FindObjectsByType<WallFoodItem>(FindObjectsSortMode.None);
            if (walls.Length == 0)
            {
                Debug.Log("[WallGnawingDirector]：场上没有被注入的墙 / 桌子（还没扫描过，或者全被吃光了）。", this);
                return;
            }

            int zombie = 0;
            int finished = 0;
            float minProgress = float.MaxValue;
            float maxProgress = float.MinValue;
            float sumProgress = 0f;

            for (int i = 0; i < walls.Length; i++)
            {
                WallFoodItem wall = walls[i];
                if (wall == null)
                {
                    continue;
                }

                if (!wall.HasNutrition)
                {
                    zombie++;
                }

                float progress = wall.EatProgress;
                if (progress >= 0.999f)
                {
                    finished++;
                }

                minProgress = Mathf.Min(minProgress, progress);
                maxProgress = Mathf.Max(maxProgress, progress);
                sumProgress += progress;
            }

            Debug.Log(
                $"[WallGnawingDirector]：场上共 {walls.Length} 个可食用墙体，" +
                $"进度 min={minProgress:F2} avg={sumProgress / walls.Length:F2} max={maxProgress:F2}，" +
                $"吃空未销毁（僵尸）={zombie}", this);

            if (zombie > 0)
            {
                Debug.LogError(
                    $"[WallGnawingDirector]：有 {zombie} 个营养为 0 却没被销毁的僵尸物体。" +
                    "下一次被蟑螂啃到时会自动补销毁；如果一直不消失，说明它们已经不在 ItemManager 的名单里了。", this);
            }
        }

        #endregion
    }
}
