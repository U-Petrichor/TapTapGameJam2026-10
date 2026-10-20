using System.Collections;
using System.Collections.Generic;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Events;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;
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

        [Tooltip("随机选点的尝试次数。被墙挡住的点直接放弃，所以实际生成的件数 ≤ 这个值")]
        [SerializeField, Min(0)] private int _spawnPointCount = 8;

        [Tooltip("选点周围的墙体检测半径，单位是格。1 = 3x3，0 = 只查自己那一格")]
        [SerializeField, Min(0)] private int _wallClearance = 1;

        [Tooltip("黑盒预制体。留空则这个分支不刷")]
        [SerializeField] private GameObject _blackBoxPrefab;

        [Tooltip("黑盒概率。和下面的食物概率相加不能超过 1，超了会自动压食物概率")]
        [SerializeField, Range(0f, 1f)] private float _blackBoxChance = 0.3f;

        [Tooltip("商品池数据资产。这里只取它里面的**食物**当刷点产物（工具 / 装扮不参与）")]
        [SerializeField] private ItemPool _itemPool;

        [Tooltip("食物概率。默认 0.3 + 0.5 = 30% 黑盒、50% 食物、20% 这个点空着")]
        [SerializeField, Range(0f, 1f)] private float _foodChance = 0.5f;

        [Tooltip("生成高度偏移，相对网格平面。摆放不对就微调这个")]
        [SerializeField] private float _spawnYOffset;

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
        /// 在网格上随机选 <see cref="_spawnPointCount"/> 个点，逐点检查周围墙体后按概率生成物品。
        ///
        /// 选点规则（按需求）：选中的点如果自己或周围 <see cref="_wallClearance"/> 圈内有墙，
        /// 就直接放弃这个点，不再找替代点 —— 所以地图很挤的时候实际生成数会少于尝试次数。
        /// 网格外一律算墙（见 <see cref="GridManager.IsBlocked"/>），所以贴边的点会被自动排掉。
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
                Debug.LogError("[NightManager]：场景里没有 GridManager，没法选点刷物品。", this);
                return;
            }

            if (_itemPool == null)
            {
                Debug.LogWarning("[NightManager]：没连商品池资产（ItemPool），夜晚不会刷任何东西。", this);
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

            // 两个概率加起来超过 1 时把食物概率压下来，免得黑盒被挤掉
            float foodChance = Mathf.Clamp(_foodChance, 0f, Mathf.Max(0f, 1f - _blackBoxChance));

            List<Vector2Int> usedCells = new List<Vector2Int>();
            int spawned = 0;
            int rejected = 0;

            for (int i = 0; i < _spawnPointCount; i++)
            {
                Vector2Int cell = new Vector2Int(
                    Random.Range(0, grid.width),
                    Random.Range(0, grid.height));

                // 同一个格子不重复刷；周围有墙的照需求直接放弃
                if (usedCells.Contains(cell) || HasWallAround(grid, cell, _wallClearance))
                {
                    rejected++;
                    continue;
                }

                usedCells.Add(cell);
                if (SpawnAt(grid, cell, foodChance, foodTemplates))
                {
                    spawned++;
                }
            }

            Debug.Log(
                $"[NightManager]：夜晚刷点完毕，尝试 {_spawnPointCount} 次，生成 {spawned} 件，" +
                $"放弃 {rejected} 个点（有墙或重复），空点 {usedCells.Count - spawned} 个。");
        }

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

        /// <summary>在某个格子上掷一次骰：黑盒 / 食物 / 什么都不刷。返回是否真的生成了东西</summary>
        private bool SpawnAt(
            GridManager grid,
            Vector2Int cell,
            float foodChance,
            List<PickableItem> foodTemplates)
        {
            GameObject prefab;
            bool isBlackBox = false;

            float roll = Random.value;
            if (roll < _blackBoxChance)
            {
                prefab = _blackBoxPrefab;
                isBlackBox = true;
            }
            else if (roll < _blackBoxChance + foodChance)
            {
                prefab = PickFoodPrefab(foodTemplates)?.gameObject;
            }
            else
            {
                return false;
            }

            if (prefab == null)
            {
                return false;
            }

            Vector2 worldXZ = grid.GridToWorld(cell);
            Vector3 position = new Vector3(worldXZ.x, grid.transform.position.y + _spawnYOffset, worldXZ.y);

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            instance.name = prefab.name;

            if (isBlackBox)
            {
                WireBlackBox(instance);
            }

            return true;
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
