using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Buffs;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 「群颚破壁」（<c>group_wall_gnawing</c>）的落地：拿到 tag 的那个夜晚，
    /// 遍历 <b>Scene/Environment</b> 下的所有子物体，把带 <see cref="MeshRenderer"/> 的统统注入
    /// <see cref="WallFoodItem"/>，让墙、桌子、以及场景里的各种杂物都变成能吃的东西。
    ///
    /// <b>只监听 <see cref="GamePhaseChangedEvent"/>，全场只注入一次。</b>
    /// 每个夜里进来只看一眼：带着 tag 就注入一次并永久记下；还没解锁就什么都不做，
    /// 下一夜再看（天赋可能在白天的购物阶段才点出来，一进来就"记账失败"会让它永远不生效）。
    ///
    /// 【为什么按层级枚举而不是打射线】
    /// 射线只能打到有 Collider 的东西，而场景里大量杂物（花盆、摆件、墙面装饰）根本没有 Collider，
    /// 按层扫描会漏掉一大半。层级枚举跟物理层无关，也不关心有没有碰撞体 ——
    /// <see cref="WallFoodItem"/> 本来就不需要 Collider（格子矩形取自 Renderer.bounds）。
    ///
    /// 【注入规则】
    /// <list type="number">
    /// <item>带 <see cref="MeshRenderer"/> 的物体才注入；空物体 / 纯碰撞体壳跳过；</item>
    /// <item><b>一旦某个物体带 MeshRenderer 就不再往下枚举</b> —— 子物体会跟着父物体一起被销毁，
    ///       单独给它们挂食物没有意义；</item>
    /// <item>带 <see cref="Interactable"/> 或 <see cref="FoodItem"/> 的物体连同整棵子树跳过 ——
    ///       门、巢穴、可拾取物不该变成食物，一个物体上也不该挂两个 FoodItem；</item>
    /// <item>包围盒超过 <see cref="_maxCellsPerObject"/> 格的大网格跳过 —— 场景里有背景大模型，
    ///       放进来一次注册十几万格会直接把内存和帧率打爆。</item>
    /// </list>
    ///
    /// 【分帧】注入是几千个物体的量级（当前场景实测约 7.6k），
    /// 所以按 <see cref="_objectsPerFrame"/> 摊到多帧，避免入夜时顿一下。
    ///
    /// 挂载位置：跟 <see cref="GridManager"/> 同一个物体（见 Assets/Prefabs/Grid.prefab）。
    /// 注意预制体不能引用场景物体，所以 Environment 根节点是按路径在运行时找的。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallGnawingDirector : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("网格。留空则先从自己 / 父物体上找，再退到 GridManager.Instance")]
        [SerializeField] private GridManager _grid;

        [Tooltip("裂纹覆盖材质（Assets/Material/WallCrack.mat）。留空则被啃了没有任何视觉反馈")]
        [SerializeField] private Material _crackMaterial;

        [Header("要注入的范围")]
        [Tooltip("要遍历的根节点路径列表（从场景根开始，例如 Scene/Environment）。" +
                 "预制体不能引用场景物体，所以一律按路径找；找不到时会退回只按最后一段名字找。" +
                 "可以填多条，重叠的会被自动去重")]
        [SerializeField] private List<string> _rootPaths = new List<string> { "Scene/Environment" };

        [Header("营养量（按包围盒体积估算）")]
        [Tooltip("每立方米给多少营养。物体会多到几千个，这个值要配得比只算墙的时候低")]
        [SerializeField, Min(0.01f)] private float _nutritionPerCubicMeter = 8f;

        [Tooltip("单件营养下限。太低的杂项就变成一口一个，至少给一点")]
        [SerializeField, Min(0.01f)] private float _minNutrition = 2f;

        [Tooltip("单件营养上限，防止整面承重墙变成无限营养池")]
        [SerializeField, Min(0.01f)] private float _maxNutrition = 120f;

        [Header("注入")]
        [Tooltip("单个物体最多占多少格。超过就跳过 —— 背景大模型盖住十几万格，放进来会卡死")]
        [SerializeField, Min(16)] private int _maxCellsPerObject = 1024;

        [Tooltip("每帧最多处理多少个物体。实测约 7.6k 个物体，400/帧 ≈ 19 帧")]
        [SerializeField, Min(16)] private int _objectsPerFrame = 400;

        [Tooltip("关闭则只在 Console 里报错，不打注入汇总")]
        [SerializeField] private bool _logSummary = true;

        [Tooltip("巡检：每 0.5 秒检查一遍已注入的物体，营养归零却没销毁的补一次销毁")]
        [SerializeField] private bool _watchDepleted = true;

        /// <summary>已经在场上的可食用物体。巡检销毁、统计都用它</summary>
        private readonly List<WallFoodItem> _injected = new List<WallFoodItem>();

        /// <summary>已经注入过了。全场只置位一次</summary>
        private bool _injectedOnce;

        private Coroutine _inject;
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
                Debug.LogError("[WallGnawingDirector]：场景里找不到 GridManager，「群颚破壁」无法注入。", this);
                return;
            }

            _injectedOnce = true;
            _inject = StartCoroutine(InjectRoutine(grid));
        }

        /// <summary>
        /// 遍历 <see cref="_rootPaths"/> 里的每个根节点，给带 MeshRenderer 的物体挂 <see cref="WallFoodItem"/>。
        /// 分帧执行，避免一次性处理几千个物体时顿一下。
        /// </summary>
        private IEnumerator InjectRoutine(GridManager grid)
        {
            float startTime = Time.realtimeSinceStartup;

            List<Transform> roots = ResolveRoots();
            if (roots.Count == 0)
            {
                Debug.LogError(
                    $"[WallGnawingDirector]：一个根节点都找不到（配置的是 {DescribePaths()}）。" +
                    "预制体不能引用场景物体，所以这里是按路径找的；" +
                    "场景根节点被改名或搬走了就要改这个列表。", this);
                _inject = null;
                yield break;
            }

            if (_logSummary)
            {
                Debug.Log(
                    $"[WallGnawingDirector]：开始遍历 {DescribeRoots(roots)}，每帧 {_objectsPerFrame} 个物体", this);
            }

            int injected = 0;
            int skippedInteractive = 0;
            int skippedNoMesh = 0;
            int skippedTooBig = 0;
            int skippedAlready = 0;
            int visited = 0;
            int processedThisFrame = 0;

            var stack = new Stack<Transform>();
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                stack.Push(roots[i]);
            }

            while (stack.Count > 0)
            {
                Transform node = stack.Pop();
                if (node == null)
                {
                    continue;
                }

                visited++;

                GameObject go = node.gameObject;

                // 门 / 巢穴 / 可拾取物：连同整棵子树跳过。
                // 一个物体上挂两个 FoodItem 会重复登记，而且这些本来就不该被啃
                if (go.GetComponent<Interactable>() != null || go.GetComponent<FoodItem>() != null)
                {
                    skippedInteractive++;
                }
                else if (go.GetComponent<MeshRenderer>() != null)
                {
                    // 有网格就是"一块可吃的东西"：注入后**不再往下递归**，
                    // 子物体会跟着它一起被销毁
                    if (go.GetComponent<WallFoodItem>() != null)
                    {
                        skippedAlready++;
                    }
                    else if (TryInject(go, grid))
                    {
                        injected++;
                    }
                    else
                    {
                        skippedTooBig++;
                    }
                }
                else
                {
                    // 空物体 / 纯碰撞体壳 / 纯容器：自己不能吃，继续往下找
                    skippedNoMesh++;

                    int count = node.childCount;
                    for (int i = 0; i < count; i++)
                    {
                        stack.Push(node.GetChild(i));
                    }
                }

                if (++processedThisFrame < _objectsPerFrame)
                {
                    continue;
                }

                processedThisFrame = 0;
                yield return null;
            }

            _inject = null;

            StartWatchdog();

            if (_logSummary)
            {
                int totalFoods = FoodManager.Instance != null ? FoodManager.Instance.Count : 0;
                Debug.Log(
                    $"[WallGnawingDirector]：遍历 {visited} 个节点，注入 {injected} 个；" +
                    $"跳过 无网格 {skippedNoMesh} / 可交互或已是食物 {skippedInteractive} / " +
                    $"体积过大或取不到包围盒 {skippedTooBig} / 已注入 {skippedAlready}；" +
                    $"场上食物共 {totalFoods} 件，耗时 {Time.realtimeSinceStartup - startTime:F2}s", this);
            }
        }

        /// <summary>给一个物体挂上 <see cref="WallFoodItem"/>。返回是否真的挂了。</summary>
        private bool TryInject(GameObject target, GridManager grid)
        {
            // 幂等：重复注入没有意义，而且会多挂一次 Awake / 一次注册
            if (target.GetComponent<WallFoodItem>() != null)
            {
                return false;
            }

            if (!TryGetWorldBounds(target, out Bounds bounds))
            {
                return false;
            }

            // 体积预检：背景大模型动辄覆盖整张图，交给 FoodManager 拒收也行，
            // 但在挂组件之前就挡掉更干净
            Vector2 cellSize = grid.CellSize;
            if (cellSize.x > 0f && cellSize.y > 0f)
            {
                int cellsX = Mathf.CeilToInt(bounds.size.x / cellSize.x) + 1;
                int cellsY = Mathf.CeilToInt(bounds.size.z / cellSize.y) + 1;
                if ((long)cellsX * cellsY > _maxCellsPerObject)
                {
                    return false;
                }
            }

            WallFoodItem food = target.AddComponent<WallFoodItem>();
            food.Configure(EstimateNutrition(bounds), _crackMaterial);
            _injected.Add(food);
            return true;
        }

        /// <summary>
        /// 营养量按包围盒体积估：承重墙比一个小摆件耐啃，但整体会小很多 ——
        /// 因为注入的物体从几百个涨到了几千个，配得跟以前一样会让营养彻底白菜价。
        /// </summary>
        private float EstimateNutrition(Bounds bounds)
        {
            float volume = Mathf.Max(0f, bounds.size.x) * Mathf.Max(0f, bounds.size.y) * Mathf.Max(0f, bounds.size.z);
            return Mathf.Clamp(volume * _nutritionPerCubicMeter, _minNutrition, _maxNutrition);
        }

        /// <summary>
        /// 取物体的世界包围盒：优先它自己的 MeshRenderer，其次子物体的 Renderer，最后才是 Collider。
        /// 顺序很重要 —— 很多杂物没有 Collider，而且父物体上的 Collider 往往是整块触发区，
        /// 拿它算"占哪些格子"会大得离谱。
        /// </summary>
        private static bool TryGetWorldBounds(GameObject target, out Bounds bounds)
        {
            MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                bounds = meshRenderer.bounds;
                return true;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
                return true;
            }

            Collider collider = target.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        /// <summary>
        /// 把 <see cref="_rootPaths"/> 解析成实际的 Transform 列表。
        ///
        /// 两件事：
        /// <list type="bullet">
        /// <item>找不到的路径打一条 warning 就跳过，不影响其它路径（某个场景没有这块就用不上）；</item>
        /// <item>互相包含的路径去重，只保留最外层 —— 不然同一棵子树会被遍历两遍
        ///       （虽然 <see cref="TryInject"/> 幂等、不会重复注入，但白跑 7.8k 个节点没必要）。</item>
        /// </list>
        /// </summary>
        private List<Transform> ResolveRoots()
        {
            var resolved = new List<Transform>();

            if (_rootPaths == null)
            {
                return resolved;
            }

            for (int i = 0; i < _rootPaths.Count; i++)
            {
                string path = _rootPaths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                Transform found = FindByPath(path);
                if (found == null)
                {
                    Debug.LogWarning($"[WallGnawingDirector]：找不到注入路径「{path}」，已跳过。", this);
                    continue;
                }

                if (!resolved.Contains(found))
                {
                    resolved.Add(found);
                }
            }

            // 去掉被别的根包含的条目（保留最外层）
            for (int i = resolved.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < resolved.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    if (resolved[i].IsChildOf(resolved[j]))
                    {
                        resolved.RemoveAt(i);
                        break;
                    }
                }
            }

            return resolved;
        }

        /// <summary>
        /// 按路径找物体。找不到完整路径时退回只按最后一段名字找 ——
        /// 场景之间的层级不一样（GameScene 是 Scene/Environment，TestNight 里 Environment 直接是根物体），
        /// 只要求"有个叫 Environment 的根"，比死磕完整路径好用。
        /// </summary>
        private static Transform FindByPath(string path)
        {
            // GameObject.Find 支持 "A/B" 这种层级路径，但要求沿途物体都是激活的
            GameObject found = GameObject.Find(path);
            if (found != null)
            {
                return found.transform;
            }

            int slash = path.LastIndexOf('/');
            if (slash >= 0 && slash < path.Length - 1)
            {
                found = GameObject.Find(path.Substring(slash + 1));
            }

            return found != null ? found.transform : null;
        }

        private string DescribePaths()
        {
            return _rootPaths == null || _rootPaths.Count == 0
                ? "（空）"
                : string.Join(" / ", _rootPaths);
        }

        private static string DescribeRoots(List<Transform> roots)
        {
            string text = string.Empty;

            for (int i = 0; i < roots.Count; i++)
            {
                if (i > 0)
                {
                    text += " / ";
                }

                Transform node = roots[i];
                string path = node.name;
                for (Transform parent = node.parent; parent != null; parent = parent.parent)
                {
                    path = parent.name + "/" + path;
                }

                text += path;
            }

            return text;
        }

        private static bool HasWallGnawingTag()
        {
            PlayerBuffs buffs = PlayerBuffs.Current;
            return buffs != null && buffs.Has(BuffKey.FlagGroupWallGnawing);
        }

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

        #region 巡检

        /// <summary>
        /// 每 0.5 秒扫一遍已经注入的物体，营养归零却没销毁的补一次销毁。
        ///
        /// 这不是主路径：主路径是 <see cref="FoodItem.Consume"/> 啃到 0 的那一帧就安排销毁。
        /// 巡检的作用是「兜底 + 自愈」—— 只要出现营养 0 却还活着的实例，最多半秒内一定被清掉。
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
                    WallFoodItem item = _injected[i];

                    // 已经销毁的（吃空、或场景卸载）从名单里摘掉
                    if (item == null)
                    {
                        _injected.RemoveAt(i);
                        continue;
                    }

                    item.DestroyIfDepleted();
                }
            }
        }

        #endregion

        #region 排查 / 测试

        /// <summary>
        /// 不用等到夜里、也不看有没有 tag，直接注入一遍。
        /// 调手感 / 裂纹效果时用它，不然每次都要等到入夜 + 点天赋。
        /// </summary>
        [ContextMenu("立即注入（测试用，忽略 tag 和阶段）")]
        private void DebugInjectNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallGnawingDirector]：注入要往场景物体上 AddComponent，只能在 Play 模式跑。");
                return;
            }

            if (_inject != null)
            {
                Debug.LogWarning("[WallGnawingDirector]：上一轮注入还没结束。");
                return;
            }

            GridManager grid = ResolveGrid();
            if (grid == null)
            {
                Debug.LogError("[WallGnawingDirector]：场景里找不到 GridManager。", this);
                return;
            }

            _injectedOnce = true;
            _inject = StartCoroutine(InjectRoutine(grid));
        }

        /// <summary>
        /// 排查「东西啃完了却还在」：把场上所有被注入的物体统计一遍。
        ///
        /// 重点看 <b>僵尸数</b>：营养已经是 0 却还没被销毁的实例，正常必须是 0。
        /// 还要看 <b>进度分布</b>：如果几千个物体都卡在很低的进度，那不是 bug，
        /// 是食物源太多、蟑螂摊得太散 —— 调小 <see cref="_nutritionPerCubicMeter"/> 就行。
        /// </summary>
        [ContextMenu("统计已注入的物体（排查用）")]
        private void DebugReportWalls()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WallGnawingDirector]：统计要读运行时状态，进 Play 模式再点。");
                return;
            }

            WallFoodItem[] items = FindObjectsByType<WallFoodItem>(FindObjectsSortMode.None);
            if (items.Length == 0)
            {
                Debug.Log("[WallGnawingDirector]：场上没有被注入的物体（还没注入过，或者全被吃光了）。", this);
                return;
            }

            int zombie = 0;
            int finished = 0;
            float minProgress = float.MaxValue;
            float maxProgress = float.MinValue;
            float sumProgress = 0f;
            int counted = 0;

            for (int i = 0; i < items.Length; i++)
            {
                WallFoodItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                counted++;

                if (!item.HasNutrition)
                {
                    zombie++;
                }

                float progress = item.EatProgress;
                if (progress >= 0.999f)
                {
                    finished++;
                }

                minProgress = Mathf.Min(minProgress, progress);
                maxProgress = Mathf.Max(maxProgress, progress);
                sumProgress += progress;
            }

            if (counted == 0)
            {
                Debug.Log("[WallGnawingDirector]：场上的可食用物体都已经被吃光了。", this);
                return;
            }

            Debug.Log(
                $"[WallGnawingDirector]：场上共 {counted} 个可食用物体，" +
                $"进度 min={minProgress:F2} avg={sumProgress / counted:F2} max={maxProgress:F2}，" +
                $"已吃满 {finished}，吃空未销毁（僵尸）={zombie}", this);

            if (zombie > 0)
            {
                Debug.LogError(
                    $"[WallGnawingDirector]：有 {zombie} 个营养为 0 却没被销毁的僵尸物体。" +
                    "下一次被蟑螂啃到时会自动补销毁；如果一直不消失，说明它们已经不在 FoodManager 的索引里了。", this);
            }
        }

        private void OnValidate()
        {
            if (_maxNutrition < _minNutrition)
            {
                _maxNutrition = _minNutrition;
            }

            if (_objectsPerFrame < 16)
            {
                _objectsPerFrame = 16;
            }

            if (_maxCellsPerObject < 16)
            {
                _maxCellsPerObject = 16;
            }
        }

        #endregion
    }
}
