using System;
using TapTapGameJam.Cockroach;
using UnityEngine;


namespace TapTapGameJam.Core
{

    public enum TileState
    {
        TileEmpty,
        TileWall
    }

    /// <summary>
    /// 信息素通道。PFood / PHome / PPlayer / PDanger 四个场各对应一路。
    /// </summary>
    public enum PheromoneKind
    {
        None,
        /// <summary>觅食信息素：发现食物后沿途留下的"这里有吃的"</summary>
        Food,
        /// <summary>归巢信息素：带着食物回巢时留下的路标</summary>
        Home,
        /// <summary>玩家通道：玩家留下的气味，衰减速率独立</summary>
        Player,
        /// <summary>危险通道：有看守 / 死过蟑螂的地方</summary>
        Danger
    }

    /// <summary>
    /// 网格射线的一次命中结果。
    /// </summary>
    public class GridRayHit
    {
        /// <summary>这次射线是否撞到了 TileWall</summary>
        public bool IsBlocked { get; set; }

        /// <summary>撞到的（或射线终点所在的）格子坐标</summary>
        public Vector2Int HitPos { get; set; }

        /// <summary>从起点到命中点的距离，单位是格子数。没撞到时为射线的实际总长</summary>
        public float Distance { get; set; }

        /// <summary>命中面的法线（指向射线来的一侧）。没撞到时为 zero</summary>
        public Vector2 HitNormal { get; set; }
    }

    public class GridManager : MonoBehaviour
    {
        
        [Header("网格配置")] 
        [Tooltip("网格的列数 (X轴方向)")] public int width = 10;
        [Tooltip("网格的行数 (Z轴方向)")] public int height = 10;

        /// <summary>本物体的 Renderer。网格覆盖的世界矩形由它的 bounds 决定，不再假设 mesh 是哪个</summary>
        private Renderer _renderer;

        /// <summary>上一次算 footprint 时的 transform 和网格尺寸，用来判断要不要重算</summary>
        private Matrix4x4 _cachedTRS;
        private int _cachedWidth = int.MinValue;
        private int _cachedHeight = int.MinValue;

        [Header("墙体探测")] [Tooltip("墙壁所在的物理层，TraceMapStateFromWall 只认这一层")]
        public LayerMask wallLayer;

        public float[,] PFood;
        public float[,] PHome;
        public float[,] PPlayer;
        public float[,] PDanger;
        public TileState[,] MapState;

        // 单元格实际物理尺寸
        private float _cellWidth;
        private float _cellHeight;

        // ============================ 坐标约定 ============================
        // 1. 网格覆盖的世界矩形 = 本物体 Renderer.bounds 在 XZ 上的投影。
        // 2. 格子 (0,0) 在矩形的 (min.x, min.z) 角 —— 也就是 x 最小、z 最小的那个角。
        //    x 沿世界 +X 增长，y 沿世界 +Z 增长。
        // 3. 格子中心 = WorldOrigin + (格子坐标 + 0.5) * CellSize。
        //
        // 为什么用 bounds 而不是 localScale*10：
        //   bounds 已经包含了 mesh 的本地尺寸和本地朝向（Plane 平铺在 XZ、Quad 在 XY）、
        //   mesh 的 pivot 偏移、以及物体自身的 scale/rotation。
        //   所以 Plane(10x10) 和 Quad(1x1，需绕 X 转 90°) 都能一次算对，
        //   既不需要知道用的是哪个 mesh，也不需要知道 pivot 在不在中心。
        //
        // 因此 shader 里也一律不用 mesh 的 uv：
        //   Plane 的 uv 在 XZ 平面、Quad 的 uv 在 XY 平面，两者 v 的朝向还不一样；
        //   用 positionWS 反算网格坐标可以完全绕开这些差异。
        // =================================================================

        /// <summary>网格覆盖的世界矩形左下角（x 最小、z 最小的角），世界 XZ 坐标</summary>
        public Vector2 WorldOrigin { get; private set; }

        /// <summary>网格覆盖的世界矩形尺寸，单位是世界单位</summary>
        public Vector2 WorldSpan { get; private set; }

        /// <summary>
        /// 单元格的实际物理尺寸。x = 列宽（世界 X 方向），y = 行高（世界 Z 方向）
        /// </summary>
        public Vector2 CellSize => new Vector2(_cellWidth, _cellHeight);

        
        private bool IsValid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        /// <summary>
        /// 取本物体在世界 XZ 上占的矩形。返回 false = 这个物体上没有 Renderer，算不出来。
        /// </summary>
        public bool TryGetFootprint(out Vector2 origin, out Vector2 span)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer == null)
            {
                origin = Vector2.zero;
                span = Vector2.one;
                return false;
            }

            Bounds b = _renderer.bounds;
            origin = new Vector2(b.min.x, b.min.z);
            span = new Vector2(Mathf.Max(b.size.x, 1e-4f), Mathf.Max(b.size.z, 1e-4f));
            return true;
        }

        /// <summary>
        /// 保证 WorldOrigin / WorldSpan / 格子尺寸是当前 transform 和网格尺寸下算出来的。
        /// 物体被移动或缩放过就会重算；没变的话每帧只多比较一个矩阵，开销可忽略。
        /// </summary>
        public void EnsureFootprint()
        {
            Matrix4x4 trs = transform.localToWorldMatrix;
            if (trs == _cachedTRS && width == _cachedWidth && height == _cachedHeight)
            {
                return;
            }

            _cachedTRS = trs;
            _cachedWidth = width;
            _cachedHeight = height;

            if (!TryGetFootprint(out Vector2 origin, out Vector2 span))
            {
                return;
            }

            WorldOrigin = origin;
            WorldSpan = span;
            _cellWidth = span.x / Mathf.Max(width, 1);
            _cellHeight = span.y / Mathf.Max(height, 1);
        }

        /// <summary>
        /// 获取网格左下角的世界 XZ 坐标（格子 (0,0) 外侧的那个角）
        /// </summary>
        private Vector2 GetGridOrigin()
        {
            EnsureFootprint();
            return WorldOrigin;
        }

        private void OnDrawGizmos()
        {
            // 和运行时用完全同一套约定画：这组线应该正好贴住 mesh 的外边缘。
            // 在 Scene 视图里看到线跑到 mesh 外面或者只盖住一半，就说明物体没放平（见 Awake 的警告）。
            if (!TryGetFootprint(out Vector2 origin, out Vector2 span))
            {
                return;
            }

            float cW = span.x / Mathf.Max(width, 1);
            float cH = span.y / Mathf.Max(height, 1);
            Vector3 corner = new Vector3(origin.x, transform.position.y, origin.y);

            Gizmos.color = Color.cyan;
            for (int x = 0; x <= width; x++)
            {
                Vector3 start = corner + new Vector3(x * cW, 0f, 0f);
                Gizmos.DrawLine(start, start + new Vector3(0f, 0f, span.y));
            }

            for (int y = 0; y <= height; y++)
            {
                Vector3 start = corner + new Vector3(0f, 0f, y * cH);
                Gizmos.DrawLine(start, start + new Vector3(span.x, 0f, 0f));
            }
        }

        private static GridManager _instance;

        public static GridManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GridManager>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            _instance = this;
            InitGrid();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 信息素刷新（衰减）间隔，单位是 FixedUpdate 的次数。
        /// </summary>
        private const int PheromoneFreshFrameInterval = 10;

        private int _pheromoneFrameCounter;
        private float _pheromoneAccumulatedDt;

        private void FixedUpdate()
        {
            _pheromoneAccumulatedDt += Time.fixedDeltaTime;

            if (++_pheromoneFrameCounter < PheromoneFreshFrameInterval)
            {
                return;
            }

            _pheromoneFrameCounter = 0;

            CockroachSettings settings = CockroachManager.Instance.cockroachSetting;
            TickPheromones(_pheromoneAccumulatedDt);

            _pheromoneAccumulatedDt = 0f;
        }

        /// <summary>
        /// 初始化网格与尺寸计算
        /// </summary>
        private void InitGrid()
        {
            _renderer = GetComponent<Renderer>();

            // 初始化二维数组
            PFood = new float[width, height];
            PHome = new float[width, height];
            PPlayer = new float[width, height];
            PDanger = new float[width, height];
            MapState = new TileState[width, height];

            // 强制按当前 transform 算一遍世界范围
            _cachedTRS = default;
            _cachedWidth = int.MinValue;
            _cachedHeight = int.MinValue;
            EnsureFootprint();
            ValidatePlacement();

            TraceMapStateFromWall();

            Debug.Log($"[GridManager] 网格 {width}x{height}；世界范围 x[{WorldOrigin.x:F2}, {WorldOrigin.x + WorldSpan.x:F2}] z[{WorldOrigin.y:F2}, {WorldOrigin.y + WorldSpan.y:F2}]；格子 {_cellWidth:F3} x {_cellHeight:F3}");
        }

        private void Start()
        {
            TraceMapStateFromWall();
        }

        /// <summary>
        /// 检查：网格只能铺在一个水平面上
        /// </summary>
        private void ValidatePlacement()
        {
            if (_renderer == null)
            {
                Debug.LogError($"[GridManager] {name} 上没有 Renderer。网格需要一个渲染物体（Plane 或 Quad）来提供世界范围。", this);
                return;
            }

            Bounds b = _renderer.bounds;
            float flat = b.size.y;
            float wide = Mathf.Max(b.size.x, b.size.z);
            if (flat > wide * 0.01f)
            {
                Debug.LogWarning($"[GridManager] {name} 看起来不是平放的（bounds 高度 {flat:F3}，平面尺寸 {wide:F3}）。Plane 不需要旋转；Quad 需要 Rotation.x = 90。", this);
            }
        }

        /// <summary>
        /// 从墙体物理碰撞重新生成 mapState。
        /// 在每个格子中心的正上方往下打一条射线，打到 wallLayer 就记为 TileWall。
        /// 注意：需要场景里的墙 Collider 已经就位。运行时摆放/移动了墙之后再调用一次即可刷新。
        /// </summary>
        [ContextMenu("重新探测墙体")]
        public void TraceMapStateFromWall()
        {
            int wallCount = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 center = GridToWorld(new Vector2Int(x, y));
                    Vector3 start = new Vector3(center.x, transform.position.y + 100.0f, center.y);

                    bool isWall = Physics.Raycast(start, Vector3.down, 200.0f, wallLayer);

                    MapState[x, y] = isWall ? TileState.TileWall : TileState.TileEmpty;
                    if (isWall)
                    {
                        wallCount++;
                    }
                }
            }

            Debug.Log($"[GridManager]：墙体探测完成，{width * height} 个格子中有 {wallCount} 个是墙");

            // 墙体变了，离墙距离场必须重算
            RefreshWallDistance();
        }

        /// <summary>
        /// 网格射线步进检测（2D DDA，类似 Amanatides &amp; Woo 算法）。
        /// 起点所在格子会被检测（如果一开始就在墙里，Distance 为 0，法线为 zero）。
        /// </summary>
        /// <param name="worldPos">射线起点（世界 XZ 平面）</param>
        /// <param name="direction">射线方向（世界 XZ 平面，不需要归一化）</param>
        /// <param name="maxDistance">最大行进距离，单位是<b>世界单位</b>（不是格子数）</param>
        public GridRayHit RayHitGrid(Vector2 worldPos, Vector2 direction, float maxDistance)
        {
            var result = new GridRayHit();

            // 把起点世界坐标换成"格子坐标系"：整数部分就是格子索引
            Vector2 origin = GetGridOrigin();
            float px = (worldPos.x - origin.x) / _cellWidth;
            float py = (worldPos.y - origin.y) / _cellHeight;

            // 方向也换到格子坐标系：gx 表示"沿 x 走 1 格时，格子坐标变化多少"
            float gx = direction.x / _cellWidth;
            float gy = direction.y / _cellHeight;
            float glen = Mathf.Sqrt(gx * gx + gy * gy);

            if (glen < 1e-6f)
            {
                // 方向退化：只检测起点所在格子
                Vector2Int only = new Vector2Int(Mathf.FloorToInt(px), Mathf.FloorToInt(py));
                result.HitPos = only;
                result.IsBlocked = IsBlocked(only);
                return result;
            }

            // 关键：归一化必须在"格子坐标系"里做，这样参数 t 就直接是"走了几格"。
            // 若在世界空间归一化后再除以格子尺寸，格子不是正方形时步进长度就不一致了。
            gx /= glen;
            gy /= glen;

            // 世界空间里每前进一个 t，实际走了多少距离（DDA 单步长度）
            float stepWorldLen = Mathf.Sqrt(
                (gx * _cellWidth) * (gx * _cellWidth) +
                (gy * _cellHeight) * (gy * _cellHeight));

            int cellX = Mathf.FloorToInt(px);
            int cellY = Mathf.FloorToInt(py);

            // 起点就在墙里，立刻返回
            if (IsBlocked(new Vector2Int(cellX, cellY)))
            {
                result.IsBlocked = true;
                result.HitPos = new Vector2Int(cellX, cellY);
                result.Distance = 0f;
                return result;
            }

            // step 是格子索引的步进方向；tDelta 是走满一整格在参数 t 上需要多少
            int stepX = gx > 0f ? 1 : (gx < 0f ? -1 : 0);
            int stepY = gy > 0f ? 1 : (gy < 0f ? -1 : 0);
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / gx) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / gy) : float.PositiveInfinity;

            // tMaxX / tMaxY：从起点出发，下一次跨过 x / y 方向格子边界的参数 t
            float tMaxX = stepX > 0 ? (cellX + 1 - px) / gx
                : stepX < 0 ? (cellX - px) / gx
                : float.PositiveInfinity;
            float tMaxY = stepY > 0 ? (cellY + 1 - py) / gy
                : stepY < 0 ? (cellY - py) / gy
                : float.PositiveInfinity;

            // t 单位是格，乘以单步世界长度才是 worldDistance
            float maxT = maxDistance / stepWorldLen;
            float lastT = 0f;

            // 每循环一次跨越一条格子边界。格子数有限，这里再加一层计数上限兜底，防止浮点异常导致死循环
            int guard = (width + height) * 4 + 16;
            while (guard-- > 0)
            {
                float t;
                Vector2 normal;

                if (tMaxX < tMaxY)
                {
                    t = tMaxX;
                    cellX += stepX;
                    tMaxX += tDeltaX;
                    // 跨过的是 x 方向的边界面，法线沿 x，指向射线来的一侧
                    normal = new Vector2(-Mathf.Sign(gx), 0f);
                }
                else
                {
                    t = tMaxY;
                    cellY += stepY;
                    tMaxY += tDeltaY;
                    normal = new Vector2(0f, -Mathf.Sign(gy));
                }

                if (t > maxT)
                {
                    // 还没走到墙就超出预算了，终点停在预算处
                    result.HitPos = new Vector2Int(cellX, cellY);
                    result.Distance = maxDistance;
                    return result;
                }

                // 走出网格：贴边结束，不算撞墙
                if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height)
                {
                    result.HitPos = new Vector2Int(Mathf.Clamp(cellX, 0, width - 1), Mathf.Clamp(cellY, 0, height - 1));
                    result.Distance = t * stepWorldLen;
                    return result;
                }

                if (MapState[cellX, cellY] == TileState.TileWall)
                {
                    result.IsBlocked = true;
                    result.HitPos = new Vector2Int(cellX, cellY);
                    result.Distance = t * stepWorldLen;
                    result.HitNormal = normal;
                    return result;
                }

                lastT = t;
            }

            Debug.LogWarning("[GridManager]：RayHitGrid 触发了步进上限，请检查 maxDistance 或网格尺寸");
            result.HitPos = new Vector2Int(Mathf.Clamp(cellX, 0, width - 1), Mathf.Clamp(cellY, 0, height - 1));
            result.Distance = lastT * stepWorldLen;
            return result;
        }

        /// <summary>
        /// 两点之间是否有墙挡住（视线检测）。起点和终点所在格子本身不参与判定。
        /// </summary>
        public bool LosClear(Vector2 worldFrom, Vector2 worldTo)
        {
            Vector2 delta = worldTo - worldFrom;
            float distance = delta.magnitude;
            if (distance < 1e-4f)
            {
                return true;
            }

            return !RayHitGrid(worldFrom, delta, distance).IsBlocked;
        }

        /// <summary>
        /// 越界一律算"阻挡"，方便蟑螂把"地图外"和"墙"当同一种东西处理
        /// </summary>
        public bool IsBlocked(Vector2Int pos)
        {
            if (!IsValid(pos.x, pos.y))
            {
                return true;
            }

            return MapState[pos.x, pos.y] == TileState.TileWall;
        }

        /// <summary>
        /// 1. 网格坐标转物理世界坐标（返回格子中心点，XZ 平面）
        /// </summary>
        public Vector2 GridToWorld(Vector2Int gridPos)
        {
            Vector2 origin = GetGridOrigin();
            float x = origin.x + (gridPos.x + 0.5f) * _cellWidth;
            float z = origin.y + (gridPos.y + 0.5f) * _cellHeight;
            return new Vector2(x, z);
        }

        /// <summary>
        /// 2. 物理世界坐标（XZ 平面）转网格坐标
        /// </summary>
        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            Vector2 origin = GetGridOrigin();
            int x = Mathf.FloorToInt((worldPos.x - origin.x) / _cellWidth);
            int y = Mathf.FloorToInt((worldPos.y - origin.y) / _cellHeight);

            // 限制在网格边界内
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            return new Vector2Int(x, y);
        }

        #region 信息素场（PFood / PHome / PPlayer / PDanger）

        /// <summary>
        /// 格子中心到"最近墙体"的距离（单位：格），贴墙惩罚用。
        /// 0 = 这格本身是墙。墙体变化后需要重新调用 RefreshWallDistance()。
        /// </summary>
        public float[,] WallDistance { get; private set; }

        private static float[,] FieldOf(PheromoneKind kind, GridManager g)
        {
            switch (kind)
            {
                case PheromoneKind.Food: return g.PFood;
                case PheromoneKind.Home: return g.PHome;
                case PheromoneKind.Player: return g.PPlayer;
                case PheromoneKind.Danger: return g.PDanger;
                default: return null;
            }
        }

        /// <summary>
        /// 采样某通道在某世界坐标处的强度
        /// </summary>
        public float SamplePheromone(PheromoneKind kind, Vector2 worldPos)
        {
            float[,] field = FieldOf(kind, this);
            if (field == null)
            {
                return 0f;
            }

            return SampleBilinear(field, worldPos);
        }

        /// <summary>
        /// 按格子坐标采样
        /// </summary>
        public float SamplePheromone(PheromoneKind kind, Vector2Int cell)
        {
            float[,] field = FieldOf(kind, this);
            if (field == null || !IsValid(cell.x, cell.y))
            {
                return 0f;
            }

            return field[cell.x, cell.y];
        }

        /// <summary>
        /// 写入信息素。以 worldPos 为圆心、radius 为世界空间半径，在网格上刷一个圆形区域：
        /// 圆心权重 1，向外按抛物线衰减到边缘的 0（中心强、边缘淡）。
        /// 写入采用"刷新最大值"而不是累加，避免同一只蟑螂来回走把格子叠爆。
        /// </summary>
        /// <param name="radius">
        /// 刷子半径，世界单位。&lt;= 0 时退化成只写 worldPos 所在的那一格。
        /// </param>
        public void WritePheromone(PheromoneKind kind, Vector2 worldPos, float intensity, float radius)
        {
            if (kind == PheromoneKind.None || intensity <= 0f)
            {
                return;
            }

            float maxPheromone = CockroachManager.Instance.cockroachSetting.MarkerMax;
            
            float[,] field = FieldOf(kind, this);
            if (field == null)
            {
                return;
            }

            Vector2 origin = GetGridOrigin();
            float gxf = (worldPos.x - origin.x) / _cellWidth;
            float gyf = (worldPos.y - origin.y) / _cellHeight;

            // 半径为 0：只写单格
            if (radius <= 0f)
            {
                int cx = Mathf.FloorToInt(gxf);
                int cy = Mathf.FloorToInt(gyf);
                if (IsValid(cx, cy) && intensity > field[cx, cy])
                {
                    field[cx, cy] = Mathf.Min(intensity, maxPheromone);
                }

                return;
            }

            // 世界空间的圆在网格空间是椭圆（格子非正方形时），所以 x / y 两个方向的半径分开算
            float rx = radius / _cellWidth;
            float ry = radius / _cellHeight;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(gxf - rx));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(gxf + rx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(gyf - ry));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(gyf + ry));

            for (int y = y0; y <= y1; y++)
            {
                // 格子中心到刷子中心的偏移，除以半径 → 归一化的椭圆距离
                float dy = (y + 0.5f - gyf) / ry;

                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - gxf) / rx;
                    float t2 = dx * dx + dy * dy;
                    if (t2 >= 1f)
                    {
                        continue;
                    }

                    // 抛物线衰减：圆心 = 1，边缘 = 0
                    PutMax(field, x, y, intensity * (1f - t2));
                }
            }
        }

        private static void PutMax(float[,] field, int cx, int cy, float value)
        {
            if (value <= 0f || cx < 0 || cy < 0 || cx >= field.GetLength(0) || cy >= field.GetLength(1))
            {
                return;
            }

            if (value > field[cx, cy])
            {
                field[cx, cy] = Mathf.Min(value, CockroachManager.Instance.cockroachSetting.MarkerMax);
            }
        }

        /// <summary>
        /// 把一个通道在半径内整体清空 / 按比例削弱。factor=0 即彻底清空。
        /// 食物被吃空时用它抹掉这一带的觅食信息素。
        /// </summary>
        public void ClearPheromoneInRadius(PheromoneKind kind, Vector2 worldCenter, float radius, float factor = 0f)
        {
            if (kind == PheromoneKind.None || radius <= 0f)
            {
                return;
            }

            float[,] field = FieldOf(kind, this);
            if (field == null)
            {
                return;
            }

            Vector2 origin = GetGridOrigin();
            float cxCenter = (worldCenter.x - origin.x) / _cellWidth;
            float cyCenter = (worldCenter.y - origin.y) / _cellHeight;
            float rCells = radius / Mathf.Min(_cellWidth, _cellHeight);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(cxCenter - rCells));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(cxCenter + rCells));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cyCenter - rCells));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(cyCenter + rCells));
            float r2 = rCells * rCells;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cxCenter;
                    float dy = y + 0.5f - cyCenter;
                    if (dx * dx + dy * dy > r2)
                    {
                        continue;
                    }

                    float value = field[x, y] * factor;
                    field[x, y] = value;
                }
            }
        }

        /// <summary>
        /// 危险信息素爆发：以 worldPos 为圆心刷一圈 danger（中心浓、边缘淡）。
        /// 蟑螂 / 玩家被击杀时用它警告同伴。
        /// </summary>
        public void BurstDanger(Vector2 worldPos, float intensity, float radius)
        {
            WritePheromone(PheromoneKind.Danger, worldPos, intensity, radius);
        }

        /// <summary>
        /// 线性衰减一拍，各通道衰减速率不同
        /// </summary>
        /// <param name="dt">实际经过的时间</param>
        public void TickPheromones(float dt)
        {
            CockroachSettings settings = CockroachManager.Instance.cockroachSetting;
            if (dt <= 0f)
            {
                return;
            }

            TickField(PFood, dt * settings.PheromoneDecay);
            TickField(PHome, dt * settings.PheromoneDecay);
            TickField(PDanger, dt * settings.PheromoneDecay);
            TickField(PPlayer, dt * settings.PlayerPheromoneDecay);
        }

        private void TickField(float[,] field, float decay)
        {
            if (field == null)
            {
                return;
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float v = field[x, y] - decay;
                    field[x, y] = Mathf.Max(0.0f, v);
                }
            }
        }

        /// <summary>
        /// 某世界坐标离最近墙体有多远（单位：格）。开阔处返回 >=4，贴墙接近 0。
        /// </summary>
        public float SampleWallDistance(Vector2 worldPos)
        {
            if (WallDistance == null)
            {
                RefreshWallDistance();
            }

            Vector2 origin = GetGridOrigin();
            int x = Mathf.Clamp(Mathf.FloorToInt((worldPos.x - origin.x) / _cellWidth), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((worldPos.y - origin.y) / _cellHeight), 0, height - 1);
            return WallDistance[x, y];
        }

        /// <summary>
        /// 离墙权重：贴墙 ≈ 1-avoid，开阔处 = 1。采样点的贴墙惩罚就是乘上它。
        /// </summary>
        public float WallWeight(Vector2 worldPos, float avoid)
        {
            if (avoid <= 0f)
            {
                return 1f;
            }

            float d = SampleWallDistance(worldPos);
            if (d >= 4f)
            {
                return 1f;
            }

            float t = d * 0.25f;
            return 1f - avoid * (1f - t * t);
        }

        [ContextMenu("重算离墙距离场")]
        public void RefreshWallDistance()
        {
            if (WallDistance == null)
            {
                WallDistance = new float[width, height];
            }

            // 两遍切角距离变换（chamfer 3-4）
            const float diag = 1.4142f;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    WallDistance[x, y] = MapState[x, y] == TileState.TileWall ? 0f : 1e6f;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = WallDistance[x, y];
                    if (x > 0 && WallDistance[x - 1, y] + 1f < d) d = WallDistance[x - 1, y] + 1f;
                    if (y > 0)
                    {
                        if (WallDistance[x, y - 1] + 1f < d) d = WallDistance[x, y - 1] + 1f;
                        if (x > 0 && WallDistance[x - 1, y - 1] + diag < d) d = WallDistance[x - 1, y - 1] + diag;
                        if (x < width - 1 && WallDistance[x + 1, y - 1] + diag < d) d = WallDistance[x + 1, y - 1] + diag;
                    }

                    WallDistance[x, y] = d;
                }
            }

            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    float d = WallDistance[x, y];
                    if (x < width - 1 && WallDistance[x + 1, y] + 1f < d) d = WallDistance[x + 1, y] + 1f;
                    if (y < height - 1)
                    {
                        if (WallDistance[x, y + 1] + 1f < d) d = WallDistance[x, y + 1] + 1f;
                        if (x < width - 1 && WallDistance[x + 1, y + 1] + diag < d) d = WallDistance[x + 1, y + 1] + diag;
                        if (x > 0 && WallDistance[x - 1, y + 1] + diag < d) d = WallDistance[x - 1, y + 1] + diag;
                    }

                    WallDistance[x, y] = d;
                }
            }

            // 世界边界也当成墙
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float border = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                    if (border < WallDistance[x, y])
                    {
                        WallDistance[x, y] = border;
                    }
                }
            }
        }

        private float SampleBilinear(float[,] field, Vector2 worldPos)
        {
            Vector2 origin = GetGridOrigin();
            float u = (worldPos.x - origin.x) / _cellWidth - 0.5f;
            float v = (worldPos.y - origin.y) / _cellHeight - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, height - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);
            float fx = Mathf.Clamp01(u - x0);
            float fy = Mathf.Clamp01(v - y0);

            float top = Mathf.Lerp(field[x0, y0], field[x1, y0], fx);
            float bottom = Mathf.Lerp(field[x0, y1], field[x1, y1], fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        #endregion

    }
}