using System.Collections.Generic;
using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 食物的空间索引。替代了原来的 <c>ItemManager</c>：那个类实际上只剩「枚举地图上所有食物」
    /// 这一件事在被用（蟑螂的觅食判定、<see cref="NightManager"/> 的天亮清场），
    /// 而"每只蟑螂每帧扫一遍全部食物"正是 800 只蟑螂觅食卡死的根源。
    ///
    /// 【结构】一张和 <see cref="GridManager"/> 同尺寸的二维格子表：
    /// <list type="bullet">
    /// <item><see cref="_cells"/>：每格一个列表，记录"这一格里有哪几件食物"；</item>
    /// <item><see cref="_occupied"/>：食物 → 它占了哪些格子，注销时用。</item>
    /// </list>
    /// 普通食物只占它所在的那一格；<see cref="WallFoodItem"/> 这类大体型食物占它的包围盒覆盖的一整片格子
    /// （多个格子因此会指向同一个 FoodItem，这是允许的）。
    ///
    /// 【为什么不是 Dictionary&lt;格子, 列表&gt;】
    /// 查询是"以蟑螂为中心由内向外找"，一格一次索引访问；字典要为每个格子算 key、查哈希。
    /// 15 万格的数组本身 1.2MB，比字典便宜也快得多。列表是<b>惰性创建</b>的 ——
    /// 真正有食物的格子只有一万来个，不给 15 万格各 new 一个 List。
    ///
    /// 【查询不去重】一件食物占多格时，同一次查询可能反复命中同一个对象。
    /// 查询是"从内圈往外找、命中第一个就返回"，所以重复命中根本不发生 —— 也就省掉了 HashSet。
    /// </summary>
    [DisallowMultipleComponent]
    public class FoodManager : MonoBehaviour
    {
        private static FoodManager _instance;

        /// <summary>
        /// 全局唯一入口。食物在 Start / Configure 里注册自己，而 Start 的执行顺序不保证晚于
        /// FoodManager.Awake，所以这里做惰性查找兜底。
        /// </summary>
        public static FoodManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FoodManager>();
                }

                return _instance;
            }
        }

        /// <summary>查询半径换算成格子数时的上限。防止有人把取食距离配大了把整张图扫一遍</summary>
        private const int MaxQueryCells = 48;

        /// <summary>
        /// 单件食物最多占多少格。**这是一道硬保险**：场景里有整块的背景大模型，
        /// 一旦它被当成食物注册进来，一次就是十几万个格子（1MB+ 的 List + 十几万次入桶），当场卡死。
        /// 正常情况下墙也就十几格、桌子几十格，4096 格（≈ 12.8m × 12.8m）足够宽松了。
        /// </summary>
        private const int MaxCellsPerFood = 4096;

        [Tooltip("网格。留空则先从自己 / 父物体上找，再退到 GridManager.Instance")]
        [SerializeField] private GridManager _grid;

        /// <summary>[y * width + x] → 该格里的食物。惰性创建</summary>
        private List<FoodItem>[] _cells;

        private int _width;
        private int _height;
        private Vector2 _cellSize;

        /// <summary>食物 → 它占用的格子（注销 / 排查用）</summary>
        private readonly Dictionary<FoodItem, List<Vector2Int>> _occupied =
            new Dictionary<FoodItem, List<Vector2Int>>();

        /// <summary>场上所有已登记的食物。给清场、统计这类"要全量"的地方用</summary>
        private readonly List<FoodItem> _foods = new List<FoodItem>();

        /// <summary>场上食物总数</summary>
        public int Count => _foods.Count;

        /// <summary>场上所有已登记的食物（只读视图，不要边遍历边销毁）</summary>
        public IReadOnlyList<FoodItem> AllFoods => _foods;

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region 登记

        /// <summary>
        /// 把食物登记进格子表。幂等：重复登记直接返回。
        /// 由 <see cref="FoodItem"/> 在"初始化完成"之后调用（见那边的注释：OnEnable 早于 Start）。
        /// </summary>
        public bool Register(FoodItem food)
        {
            if (food == null || !EnsureGrid())
            {
                return false;
            }

            if (_occupied.ContainsKey(food))
            {
                return true;
            }

            food.GetOccupiedCells(_grid, out Vector2Int min, out Vector2Int max);

            min = ClampCell(min);
            max = ClampCell(max);
            if (max.x < min.x || max.y < min.y)
            {
                return false;
            }

            long cellCount = (long)(max.x - min.x + 1) * (max.y - min.y + 1);
            if (cellCount > MaxCellsPerFood)
            {
                // 背景大模型之类：直接拒收。放进来会分配一个十几万长度的 List 并刷进十几万个桶，
                // 一次就能把这一帧拖到几秒
                Debug.LogWarning(
                    $"[FoodManager]：「{food.name}」占 {cellCount} 格，超过上限 {MaxCellsPerFood}，" +
                    "已跳过登记（多半是背景大网格）。如果这是有意的，调大 FoodManager.MaxCellsPerFood。", food);
                return false;
            }

            var cells = new List<Vector2Int>((int)cellCount);

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    int index = y * _width + x;

                    List<FoodItem> bucket = _cells[index];
                    if (bucket == null)
                    {
                        // 绝大多数格子只有一件食物，留 2 个容量够用
                        bucket = new List<FoodItem>(2);
                        _cells[index] = bucket;
                    }

                    bucket.Add(food);
                    cells.Add(new Vector2Int(x, y));
                }
            }

            _occupied[food] = cells;
            _foods.Add(food);
            return true;
        }

        /// <summary>把食物从格子表里摘掉。幂等：没登记过直接返回。</summary>
        public void Unregister(FoodItem food)
        {
            if (food == null || !_occupied.TryGetValue(food, out List<Vector2Int> cells))
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                List<FoodItem> bucket = _cells[cell.y * _width + cell.x];
                // 空列表留着复用，不回收：注销/登记是高频操作（尸体、夜晚刷的食物），
                // 反复 new / GC 比多留几个空 List 贵得多
                bucket?.Remove(food);
            }

            _occupied.Remove(food);
            _foods.Remove(food);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 以 <paramref name="worldPos"/> 为中心、<paramref name="radius"/> 为半径，
        /// <b>由内圈向外圈</b>找第一件还有营养的食物。
        ///
        /// 命中即返回（而不是把所有候选都收进列表）是这里性能的关键：
        /// 破壁之后墙遍地都是，通常内圈一两格就命中了，单次查询只有几十次数组访问；
        /// 而且不分配任何对象，800 只蟑螂每帧各查一次也不产生 GC。
        ///
        /// <paramref name="eatPoint"/> 返回的是命中那一格的<b>中心</b>：蟑螂对着它转向、走到够得着为止。
        /// </summary>
        public bool TryFindNearestFood(Vector2 worldPos, float radius, out FoodItem food, out Vector2 eatPoint)
        {
            food = null;
            eatPoint = default;

            if (radius <= 0f || !EnsureGrid())
            {
                return false;
            }

            Vector2Int center = _grid.WorldToGrid(worldPos);

            // 半径换算成格子数：用较小的那一边算，保证两个轴向都覆盖到
            float minCell = Mathf.Max(1e-4f, Mathf.Min(_cellSize.x, _cellSize.y));
            int radiusCells = Mathf.Clamp(Mathf.CeilToInt(radius / minCell), 0, MaxQueryCells);

            for (int r = 0; r <= radiusCells; r++)
            {
                if (r == 0)
                {
                    if (TryCell(center.x, center.y, out food, out eatPoint))
                    {
                        return true;
                    }

                    continue;
                }

                int x0 = center.x - r;
                int x1 = center.x + r;
                int y0 = center.y - r;
                int y1 = center.y + r;

                // 上下两条边（整行）
                for (int x = x0; x <= x1; x++)
                {
                    if (TryCell(x, y0, out food, out eatPoint) || TryCell(x, y1, out food, out eatPoint))
                    {
                        return true;
                    }
                }

                // 左右两条边（去掉四角，避免和上下边重复访问）
                for (int y = y0 + 1; y <= y1 - 1; y++)
                {
                    if (TryCell(x0, y, out food, out eatPoint) || TryCell(x1, y, out food, out eatPoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryCell(int x, int y, out FoodItem food, out Vector2 eatPoint)
        {
            food = null;
            eatPoint = default;

            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                return false;
            }

            List<FoodItem> bucket = _cells[y * _width + x];
            if (bucket == null)
            {
                return false;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                FoodItem candidate = bucket[i];
                // 已经吃空 / 已被销毁的（Unity 的 == null）都不算
                if (candidate == null || !candidate.HasNutrition)
                {
                    continue;
                }

                food = candidate;
                eatPoint = _grid.GridToWorld(new Vector2Int(x, y));
                return true;
            }

            return false;
        }

        /// <summary>
        /// 场上所有食物的一份<b>副本</b>。给 <see cref="NightManager.ClearFoodOnMap"/> 这种
        /// "一边遍历一边 Destroy"的地方用 —— 直接遍历 <see cref="AllFoods"/> 会在销毁时改集合。
        /// </summary>
        public List<FoodItem> GetAllFoods()
        {
            return new List<FoodItem>(_foods);
        }

        #endregion

        #region 内部

        private bool EnsureGrid()
        {
            if (_cells != null)
            {
                return true;
            }

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

            if (_grid == null)
            {
                return false;
            }

            // GridManager 的格子尺寸是它自己按 Renderer.bounds 算出来的，
            // 这里必须先催它算一遍，否则 CellSize 还是 0
            _grid.EnsureFootprint();

            Vector2 cellSize = _grid.CellSize;
            if (cellSize.x <= 0f || cellSize.y <= 0f)
            {
                return false;
            }

            _width = Mathf.Max(1, _grid.width);
            _height = Mathf.Max(1, _grid.height);
            _cellSize = cellSize;
            _cells = new List<FoodItem>[_width * _height];
            return true;
        }

        private Vector2Int ClampCell(Vector2Int cell)
        {
            return new Vector2Int(
                Mathf.Clamp(cell.x, 0, _width - 1),
                Mathf.Clamp(cell.y, 0, _height - 1));
        }

        #endregion
    }
}
