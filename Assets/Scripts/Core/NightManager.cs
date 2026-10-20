using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 「TapTapGameJam.Cockroach」既是命名空间也是类型名。
// 在 Core 命名空间里直接写 Cockroach 会被解析成命名空间，所以给类型起个别名。
using Roach = TapTapGameJam.Cockroach.Cockroach;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 夜晚生命周期：Shopping → Night 的提示文字、黑屏切换和控制恢复；
    /// 另外负责夜晚的**物品生成**和天亮的**清场**。
    ///
    /// 【物品生成】进入夜晚、黑屏还没淡出的时候，在网格上随机选若干点，每个点先用
    /// <see cref="GridManager"/> 的墙体数据检查周围一圈，被墙挡住就直接放弃这个点；
    /// 通过检查的点按概率生成黑盒 / 食物。
    /// 因为是在黑屏期间生成的，玩家看不到东西凭空出现。
    ///
    /// 【天亮清场】蟑螂和食物都只属于夜晚：
    /// - 没回巢的蟑螂全部击杀（回巢的那批留着，它们明天还要孵）；
    /// - 地上所有食物清空（背包里的不动，那是玩家的东西）。
    ///
    /// 注意：<see cref="CockNest"/> 自己也会在阶段切换时清理没回巢的蟑螂（巢穴的不变量）。
    /// 两处都跑不会出问题——<see cref="Roach.Die"/> 有 <c>_dead</c> 兜底，重复调用是空操作。
    /// </summary>
    public sealed class NightManager : MonoBehaviour
    {
        [SerializeField] private ShoppingManager _shoppingManager;
        [SerializeField] private GamePhaseManager _phaseManager;
        [SerializeField] private GameObject _fadeOverlayCanvas;
        [SerializeField] private Image _fadeImage;
        [SerializeField] private TMP_Text _fadeText;
        [SerializeField] private string _message = "潜入公司！";
        [SerializeField, Min(0f)] private float _textDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;
        private Coroutine _transition;

        #region 夜晚物品生成配置

        [Header("夜晚物品生成")]
        [Tooltip("关掉则夜晚不刷任何东西（场景里手工摆的照旧）")]
        [SerializeField] private bool _spawnItems = true;

        [Tooltip("每晚生成的物品总数")]
        [SerializeField, FormerlySerializedAs("_spawnPointCount"), Min(0)]
        private int _itemsPerNight = 8;

        [Tooltip("房间区域根物体（Scene/FoodArea）。它的每个子物体是一块 Plane + 一个 FoodSpawnArea。留空则自动找场上所有 FoodSpawnArea")]
        [SerializeField] private Transform _foodAreasRoot;

        [Tooltip("选点周围的墙体检测半径，单位是格。1 = 3x3，0 = 只查自己那一格")]
        [SerializeField, Min(0)] private int _wallClearance = 1;

        [Tooltip("在房间里随机取位置的最大重试次数。整间房都被墙占住就放弃这一件")]
        [SerializeField, Min(1)] private int _positionTries = 24;

        [Tooltip("黑盒预制体。留空则这个分支不刷")]
        [SerializeField] private GameObject _blackBoxPrefab;

        [Tooltip("黑盒概率。其余的一律刷食物，所以每晚正好是 _itemsPerNight 件")]
        [SerializeField, Range(0f, 1f)] private float _blackBoxChance = 0.3f;

        [Tooltip("商品池数据资产。这里只取它里面的**食物**当刷点产物（工具 / 装扮不参与）")]
        [SerializeField] private ItemPool _itemPool;

        [Tooltip("生成高度偏移，相对网格平面。摆放不对就微调这个")]
        [SerializeField] private float _spawnYOffset;

        [Header("食物被啃过")]
        [Tooltip("刷出来的食物随机扣掉的营养比例下限。0.3 = 少了 30%")]
        [SerializeField, Range(0f, 0.95f)] private float _foodNutritionDeductMin = 0.3f;

        [Tooltip("刷出来的食物随机扣掉的营养比例上限。0.8 = 少了 80%")]
        [SerializeField, Range(0f, 0.95f)] private float _foodNutritionDeductMax = 0.8f;

        #endregion

        #region 天亮清场配置

        [Header("天亮清场")]
        [Tooltip("进入白天时击杀没回巢的蟑螂、清空地上所有食物")]
        [SerializeField] private bool _cleanupAtDay = true;

        [Tooltip("第一个白天不清场：把场景里手工摆的食物留到第一个夜晚结束之后")]
        [SerializeField] private bool _keepHandPlacedOnFirstDay = true;

        #endregion

        /// <summary>是否已经进过一次夜晚。首日保护要靠它，否则开局就会把场景里手工摆的东西清掉</summary>
        private bool _hasEnteredNight;

        private void OnEnable() => EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            if (_transition == null) return;
            StopCoroutine(_transition);
            _transition = null;
            if (_shoppingManager != null) _shoppingManager.CompleteNightTransition();
            if (_fadeOverlayCanvas != null) _fadeOverlayCanvas.SetActive(false);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            // 天亮：先清场，再走下面原有的"收尾过渡"逻辑
            if (evt.NewPhase == GamePhase.Day)
            {
                HandleDayStarted();
            }

            if (evt.NewPhase != GamePhase.Night)
            {
                if (_transition == null) return;
                StopCoroutine(_transition);
                _transition = null;
                // ShoppingManager 已处理新阶段；不能覆盖刚启动的 Shopping 过场。
                if (evt.NewPhase != GamePhase.Shopping && _fadeOverlayCanvas != null)
                    _fadeOverlayCanvas.SetActive(false);
                return;
            }
            if (_transition != null) return;
            if (_shoppingManager == null || _phaseManager == null || _fadeOverlayCanvas == null ||
                _fadeImage == null || _fadeText == null)
            {
                Debug.LogError("[NightManager] 请配置阶段管理器、ShoppingManager 和过场 UI。", this);
                return;
            }
            _phaseManager.TimeCouldWork = false;
            _shoppingManager.BeginNightTransition();
            _transition = StartCoroutine(EnterNight());
        }

        private IEnumerator EnterNight()
        {
            _hasEnteredNight = true;

            SetAlpha(0f);
            _fadeText.text = _message;
            _fadeText.gameObject.SetActive(true);
            _fadeOverlayCanvas.SetActive(true);
            yield return new WaitForSecondsRealtime(_textDisplayDuration);
            yield return Fade(0f, 1f);

            _fadeText.gameObject.SetActive(false);
            _shoppingManager.HideRoomForNight();

            // 全黑的时候刷物品：玩家看不见东西凭空出现
            SpawnNightItems();

            Canvas.ForceUpdateCanvases();
            yield return Fade(1f, 0f);

            _fadeOverlayCanvas.SetActive(false);
            _shoppingManager.CompleteNightTransition();
            _transition = null;
            EventPipeline.Publish(new NightTransitionCompletedEvent());
        }

        private IEnumerator Fade(float from, float to)
        {
            SetAlpha(from);
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / _fadeDuration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha) => _fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));

        #region 天亮清场

        private void HandleDayStarted()
        {
            if (!_cleanupAtDay)
            {
                return;
            }

            if (!_hasEnteredNight && _keepHandPlacedOnFirstDay)
            {
                Debug.Log("[NightManager]：这是第一个白天，保留场景里手工摆的物品，不做清场。");
                return;
            }

            CleanupNightLeftovers();
        }

        /// <summary>
        /// 天亮清场：击杀没归巢的蟑螂 + 清空地上的食物。
        /// 挂在 ContextMenu 上是为了能单独点一下验证，不用真的等到天亮。
        /// </summary>
        [ContextMenu("立即清场（天亮逻辑）")]
        public void CleanupNightLeftovers()
        {
            int killed = KillRoachesOutsideNest();
            int cleared = ClearFoodOnMap();
            Debug.Log($"[NightManager] 天亮清场：击杀 {killed} 只未归巢的蟑螂，清空 {cleared} 份食物。");
        }

        /// <summary>
        /// 击杀所有"不在巢里"的蟑螂。返回击杀数量。
        ///
        /// 先收集再杀：<see cref="Roach.Die"/> 走的是 <c>Destroy</c>（帧末才真正销毁），
        /// 而巢穴名单是 <see cref="CockNest"/> 在蟑螂 OnDisable 里维护的，
        /// 不要一边遍历别人的集合一边指望它别变。
        /// </summary>
        private static int KillRoachesOutsideNest()
        {
            CockroachManager manager = CockroachManager.Instance;
            if (manager == null)
            {
                return 0;
            }

            List<Roach> targets = new List<Roach>();
            List<CockNest> nests = manager.CockNestList;
            for (int i = 0; i < nests.Count; i++)
            {
                if (nests[i] == null)
                {
                    continue;
                }

                List<Roach> roaches = nests[i].CockroachList;
                for (int j = 0; j < roaches.Count; j++)
                {
                    Roach roach = roaches[j];
                    // 巢里的留着（明天还要靠它们孵化）；已经死掉的跳过，别重复结算
                    if (roach != null && !roach.IsInside && !roach.IsDead)
                    {
                        targets.Add(roach);
                    }
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Die();
            }

            return targets.Count;
        }

        /// <summary>
        /// 清空地图上的所有食物。返回清掉的数量。
        ///
        /// 只清登记在 <see cref="FoodManager"/> 里的那批 —— 也就是"在场上"的那批：
        /// 玩家背包里的、装备槽里的、商店仓库里的都已经 SetActive(false)、不在索引里。
        ///
        /// 还要看 <see cref="FoodItem.IsClearedAtDay"/>：被「群颚破壁」注入过的墙 / 桌子
        /// 同样是 <see cref="FoodItem"/>，也在地图上，但它们是**场景本体**——
        /// 顺手清掉就等于天亮把地图拆了一半。
        /// </summary>
        private static int ClearFoodOnMap()
        {
            FoodManager foodManager = FoodManager.Instance;
            if (foodManager == null)
            {
                return 0;
            }

            GridManager grid = GridManager.Instance;
            float clearRadius = CockroachManager.Instance != null
                ? CockroachManager.Instance.cockroachSetting.EmptyClearRadius
                : 0f;

            // GetAllFoods 返回的是一份副本，所以在遍历里 Destroy 是安全的
            List<FoodItem> foods = foodManager.GetAllFoods();
            int cleared = 0;
            for (int i = 0; i < foods.Count; i++)
            {
                FoodItem food = foods[i];
                if (food == null || !food.IsClearedAtDay)
                {
                    continue;
                }

                // 引路的觅食信息素一起抹掉，否则下一夜蟑螂还会往空地上跑
                if (grid != null && clearRadius > 0f)
                {
                    grid.ClearPheromoneInRadius(
                        PheromoneKind.Food,
                        Roach.FlatOf(food.transform.position),
                        clearRadius,
                        0f);
                }

                Destroy(food.gameObject);
                cleared++;
            }

            return cleared;
        }

        #endregion

        #region 夜晚物品生成

        /// <summary>
        /// 一个可刷物品的房间：范围来自 Scene/FoodArea 下某个子物体上 Plane 的 bounds，
        /// 权重来自它挂的 <see cref="FoodSpawnArea.areaRate"/>。
        /// </summary>
        private struct FoodRoom
        {
            /// <summary>房间在世界 XZ 平面上的矩形（世界单位）</summary>
            public Vector2 Min;
            public Vector2 Max;

            /// <summary>加权抽房间时的权重</summary>
            public float Weight;
        }

        /// <summary>
        /// 每晚生成 <see cref="_itemsPerNight"/> 件物品：
        ///
        /// 1. 枚举 <see cref="_foodAreasRoot"/>（场景里的 Scene/FoodArea）下的每个子物体：
        ///    子物体是一块 Plane，它的 Renderer.bounds 在 XZ 上的投影就是这间房的矩形；
        ///    子物体上挂的 <see cref="FoodSpawnArea"/> 给出这间房被抽中的权重；
        /// 2. 按权重抽一间房，在房间矩形里随机取点；点落在墙里就重试，最多 <see cref="_positionTries"/> 次，
        ///    仍然不行就放弃这一件（不会跑到房间外面去）；
        /// 3. 最后决定物品类型：<see cref="_blackBoxChance"/> 概率黑盒，其余一律食物
        ///    （食物从商品池里等概率抽；某一边没配就自动退到另一边，所以每晚正好 N 件）。
        ///    食物生成后会随机扣掉 30%~80% 营养，看起来像"被人啃过"。
        /// </summary>
        [ContextMenu("立即刷一批夜晚物品（测试用）")]
        public void SpawnNightItems()
        {
            if (!_spawnItems)
            {
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null || grid.MapState == null)
            {
                Debug.LogError("[NightManager]：场景里没有 GridManager，没法判断墙，夜晚不刷物品。", this);
                return;
            }

            if (_itemPool == null)
            {
                Debug.LogWarning("[NightManager]：没连商品池资产（ItemPool），夜晚不会刷任何东西。", this);
                return;
            }

            List<FoodRoom> rooms = CollectRooms();
            if (rooms.Count == 0)
            {
                Debug.LogError(
                    "[NightManager]：一个房间都没找到。检查 Scene/FoodArea 下每个子物体是否同时挂着 " +
                    "FoodSpawnArea（权重 > 0）和带 Renderer 的 Plane。", this);
                return;
            }

            // 食物不在这里再配一份列表：直接从商品池资产里把 Category == Food 的全抽出来。
            // 商店 / 黑盒 / 夜晚生成看到的是同一份，加一件食物只改资产
            List<PickableItem> foodTemplates = _itemPool.ItemsOf(ItemCategory.Food);

            bool hasBlackBox = _blackBoxPrefab != null;
            bool hasFood = foodTemplates.Count > 0;
            if (!hasBlackBox && !hasFood)
            {
                Debug.LogWarning(
                    $"[NightManager]：商品池「{_itemPool.name}」里没有食物，黑盒预制体也没连，夜晚不会刷任何东西。",
                    this);
                return;
            }

            float spawnY = grid.transform.position.y + _spawnYOffset;

            int spawned = 0;
            int skipped = 0;

            for (int i = 0; i < _itemsPerNight; i++)
            {
                if (!TryPickRoom(rooms, out FoodRoom room))
                {
                    skipped++;
                    continue;
                }

                if (!TryPickPosition(grid, room, out Vector2 xz))
                {
                    skipped++;
                    continue;
                }

                if (SpawnAt(new Vector3(xz.x, spawnY, xz.y), foodTemplates))
                {
                    spawned++;
                }
            }

            Debug.Log(
                $"[NightManager]：夜晚生成 {spawned} / {_itemsPerNight} 件，" +
                $"参与加权的房间 {rooms.Count} 个，放弃 {skipped} 件（房间太小或被墙占满）。");
        }

        #region 房间

        /// <summary>
        /// 收集所有刷物品的房间。优先读 Inspector 上连的 <see cref="_foodAreasRoot"/> 的子物体；
        /// 没连的话兜底为"场上所有 FoodSpawnArea"，免得忘了连线就完全不刷。
        /// </summary>
        private List<FoodRoom> CollectRooms()
        {
            List<FoodRoom> rooms = new List<FoodRoom>();

            if (_foodAreasRoot != null)
            {
                for (int i = 0; i < _foodAreasRoot.childCount; i++)
                {
                    AddRoom(_foodAreasRoot.GetChild(i), rooms);
                }

                return rooms;
            }

            FoodSpawnArea[] areas = FindObjectsByType<FoodSpawnArea>(FindObjectsSortMode.None);
            if (areas.Length > 0)
            {
                Debug.LogWarning(
                    "[NightManager]：没连 Scene/FoodArea，改用「场上所有 FoodSpawnArea」兜底。建议在 Inspector 里把根物体连上。",
                    this);
            }

            for (int i = 0; i < areas.Length; i++)
            {
                AddRoom(areas[i].transform, rooms);
            }

            return rooms;
        }

        /// <summary>
        /// 把一个子节点登记成房间：权重取它（或它的子物体）上的 <see cref="FoodSpawnArea"/>，
        /// 范围取它（或它的子物体）上 Renderer 的 bounds。
        /// 权重 &lt;= 0、没有 Renderer、或者平面小到没面积，都直接跳过（方便临时关掉某间房）。
        /// </summary>
        private static void AddRoom(Transform node, List<FoodRoom> rooms)
        {
            if (node == null)
            {
                return;
            }

            FoodSpawnArea area = node.GetComponent<FoodSpawnArea>();
            if (area == null)
            {
                area = node.GetComponentInChildren<FoodSpawnArea>(true);
            }

            Renderer mesh = node.GetComponent<Renderer>();
            if (mesh == null)
            {
                mesh = node.GetComponentInChildren<Renderer>(true);
            }

            if (area == null || mesh == null || area.areaRate <= 0f)
            {
                return;
            }

            // bounds 已经包含 mesh 尺寸、物体自身的旋转与缩放，所以 Plane 只需平放（和 GridManager 同一套约定）
            Bounds bounds = mesh.bounds;
            if (bounds.size.x <= 0.0001f || bounds.size.z <= 0.0001f)
            {
                return;
            }

            rooms.Add(new FoodRoom
            {
                Min = new Vector2(bounds.min.x, bounds.min.z),
                Max = new Vector2(bounds.max.x, bounds.max.z),
                Weight = area.areaRate,
            });
        }

        /// <summary>按 areaRate 加权抽一间房</summary>
        private static bool TryPickRoom(List<FoodRoom> rooms, out FoodRoom room)
        {
            room = default;
            if (rooms == null || rooms.Count == 0)
            {
                return false;
            }

            float total = 0f;
            for (int i = 0; i < rooms.Count; i++)
            {
                total += rooms[i].Weight;
            }

            if (total <= 0f)
            {
                return false;
            }

            float roll = Random.value * total;
            for (int i = 0; i < rooms.Count; i++)
            {
                roll -= rooms[i].Weight;
                if (roll <= 0f)
                {
                    room = rooms[i];
                    return true;
                }
            }

            // 浮点误差兜底：正常走不到这里
            room = rooms[rooms.Count - 1];
            return true;
        }

        /// <summary>
        /// 在房间矩形里随机取一个位置，落在墙里就重试。重试 <see cref="_positionTries"/> 次都撞墙
        /// （比如房间被家具墙占满、或者画到地图外面去了）就返回 false，这件物品放弃。
        /// </summary>
        private bool TryPickPosition(GridManager grid, FoodRoom room, out Vector2 position)
        {
            for (int i = 0; i < _positionTries; i++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(room.Min.x, room.Max.x),
                    Random.Range(room.Min.y, room.Max.y));

                // 地图外也当墙处理，免得房间画到边界外时刷到地图外去
                if (grid.IsBlockedWorld(candidate))
                {
                    continue;
                }

                if (HasWallAround(grid, grid.WorldToGrid(candidate), _wallClearance))
                {
                    continue;
                }

                position = candidate;
                return true;
            }

            position = default;
            return false;
        }

        #endregion

        /// <summary>选点是否合格：以 <paramref name="center"/> 为中心、半径 <paramref name="radius"/> 格的方块里不能有墙</summary>
        private static bool HasWallAround(GridManager grid, Vector2Int center, int radius)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                for (int y = center.y - radius; y <= center.y + radius; y++)
                {
                    if (grid.IsBlocked(new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 在指定世界位置刷一件：按 <see cref="_blackBoxChance"/> 决定黑盒还是食物。
        /// 两边只有一边配了东西时自动退到另一边，保证"每晚 N 件"不会被空点吃掉。
        /// </summary>
        private bool SpawnAt(Vector3 position, List<PickableItem> foodTemplates)
        {
            bool wantBlackBox = Random.value < _blackBoxChance;

            GameObject prefab = wantBlackBox ? _blackBoxPrefab : PickFoodPrefab(foodTemplates)?.gameObject;
            bool isBlackBox = wantBlackBox;

            // 想刷的那种没配（没连黑盒预制体 / 商品池里没有食物）就退到另一种
            if (prefab == null)
            {
                isBlackBox = !wantBlackBox;
                prefab = isBlackBox ? _blackBoxPrefab : PickFoodPrefab(foodTemplates)?.gameObject;
            }

            if (prefab == null)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = prefab.name;

            if (isBlackBox)
            {
                WireBlackBox(instance);
            }
            else
            {
                ApplyFoodNutritionDeduction(instance);
            }

            return true;
        }

        /// <summary>
        /// 刷出来的食物要走两步才定营养（都必须在 Start 之前设，Start 会用它俩算 remainingNutrition）：
        /// 1. <b>日期权重</b>：乘 1.2^天数（<see cref="DayScaling.NutritionWeight"/>），越往后食物越肥；
        /// 2. <b>被啃过</b>：随机扣掉 <see cref="_foodNutritionDeductMin"/>~<see cref="_foodNutritionDeductMax"/> 比例。
        /// </summary>
        private void ApplyFoodNutritionDeduction(GameObject instance)
        {
            FoodItem food = instance.GetComponent<FoodItem>();
            if (food == null)
            {
                return;
            }

            food.SetSpawnNutritionMultiplier(DayScaling.NutritionWeight);

            float min = Mathf.Min(_foodNutritionDeductMin, _foodNutritionDeductMax);
            float max = Mathf.Max(_foodNutritionDeductMin, _foodNutritionDeductMax);
            float deducted = Random.Range(min, max);

            food.SetSpawnNutritionFraction(1f - deducted);
        }

        /// <summary>从商品池里的食物模板中等概率抽一个</summary>
        private static PickableItem PickFoodPrefab(List<PickableItem> foodTemplates)
        {
            if (foodTemplates == null || foodTemplates.Count == 0)
            {
                return null;
            }

            return foodTemplates[Random.Range(0, foodTemplates.Count)];
        }

        /// <summary>
        /// 给新刷出来的黑盒补上出货源：黑盒预制体上没连商品池的话，用夜晚上这一份补上，
        /// 免得刷出来的盒子扫完什么都吐不出来。已经连好的（包括配了 hidedItem 的）不覆盖。
        /// </summary>
        private void WireBlackBox(GameObject instance)
        {
            BlackBox box = instance.GetComponent<BlackBox>();
            if (box == null || box.itemPool != null)
            {
                return;
            }

            box.itemPool = _itemPool;
        }

        #endregion
    }
}
