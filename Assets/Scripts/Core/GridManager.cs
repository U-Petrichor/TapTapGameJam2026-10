using System;
using UnityEngine;


namespace TapTapGameJam.Core
{

    public enum TileState
    {
        TileEmpty,
        TileWall
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
        private bool IsValid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        /// <summary>
        /// 获取网格左下角的物理世界坐标起始点
        /// </summary>
        private Vector3 GetGridOrigin()
        {
            Vector3 pos = _proxyObject.position;
            float totalWidth = _proxyObject.localScale.x * 10f;
            float totalHeight = _proxyObject.localScale.z * 10f;

            // 假设代理物体中心在原点，计算出左下角的世界坐标
            return new Vector3(pos.x - totalWidth / 2f, pos.y, pos.z - totalHeight / 2f);
        }

        private void OnDrawGizmos()
        {
            Transform target = _proxyObject != null ? _proxyObject : transform;
            if (target == null) return;

            Vector3 pos = target.position;
            float totalWidth = target.localScale.x * 10f;
            float totalHeight = target.localScale.z * 10f;
            Vector3 origin = new Vector3(pos.x - totalWidth / 2f, pos.y, pos.z - totalHeight / 2f);

            float cW = totalWidth / width;
            float cH = totalHeight / height;

            Gizmos.color = Color.cyan;
            for (int x = 0; x <= width; x++)
            {
                Vector3 start = origin + new Vector3(x * cW, 0, 0);
                Vector3 end = start + new Vector3(0, 0, totalHeight);
                Gizmos.DrawLine(start, end);
            }

            for (int y = 0; y <= height; y++)
            {
                Vector3 start = origin + new Vector3(0, 0, y * cH);
                Vector3 end = start + new Vector3(totalWidth, 0, 0);
                Gizmos.DrawLine(start, end);
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

        [Header("网格配置")] 
        [Tooltip("网格的列数 (X轴方向)")] public int width = 10;
        [Tooltip("网格的行数 (Z轴方向)")] public int height = 10;

        private Transform _proxyObject;

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
        /// 初始化网格与尺寸计算
        /// </summary>
        private void InitGrid()
        {
            _proxyObject = transform;

            // Unity默认Plane大小为 10x10 单位，结合 localScale 计算实际物理宽高
            float totalWidth = _proxyObject.localScale.x * 10f;
            float totalHeight = _proxyObject.localScale.z * 10f;

            // 计算每个格子的宽高
            _cellWidth = totalWidth / width;
            _cellHeight = totalHeight / height;

            // 初始化二维数组
            PFood = new float[width, height];
            PHome = new float[width, height];
            PPlayer = new float[width, height];
            PDanger = new float[width, height];
            MapState = new TileState[width, height];
            TraceMapStateFromWall();

            Debug.Log($"网格初始化成功：大小 {width}x{height}，格子物理尺寸：{_cellWidth:F2} x {_cellHeight:F2}");
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
                    Vector3 center = GridToWorld(new Vector2Int(x, y));
                    Vector3 start = new Vector3(center.x, center.y + 0.5f, center.z);

                    bool isWall = Physics.Raycast(start, Vector3.up, 200.0f, wallLayer);

                    MapState[x, y] = isWall ? TileState.TileWall : TileState.TileEmpty;
                    if (isWall)
                    {
                        wallCount++;
                    }
                }
            }

            Debug.Log($"[GridManager]：墙体探测完成，{width * height} 个格子中有 {wallCount} 个是墙");
        }

        /// <summary>
        /// 网格射线步进检测（2D DDA，类似 Amanatides &amp; Woo 算法）。
        /// maxT 的单位是格子数：maxT = 5 表示"最多走 5 格"。
        /// 起点所在格子会被检测（如果一开始就在墙里，Distance 为 0，法线为 zero）。
        /// </summary>
        /// <param name="worldPos">射线起点（世界坐标）</param>
        /// <param name="direction">射线方向（世界坐标）</param>
        /// <param name="maxT">最大步进距离，单位是格子数</param>
        public GridRayHit RayHitGrid(Vector3 worldPos, Vector2 direction, float maxT)
        {
            var result = new GridRayHit();

            // 把起点世界坐标换成"格子坐标系"：整数部分就是格子索引
            Vector3 origin = GetGridOrigin();
            float px = (worldPos.x - origin.x) / _cellWidth;
            float py = (worldPos.z - origin.z) / _cellHeight;

            // 方向也换到格子坐标系：dx 表示"每走 1 格，x 变化多少"
            direction = direction.normalized;
            float dx = direction.x / _cellWidth;
            float dy = direction.y / _cellHeight;

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
            int stepX = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
            int stepY = dy > 0f ? 1 : (dy < 0f ? -1 : 0);
            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dx) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dy) : float.PositiveInfinity;

            // tMaxX / tMaxY：从起点出发，下一次跨过 x / y 方向格子边界的参数 t
            float tMaxX = stepX > 0 ? (cellX + 1 - px) / dx
                : stepX < 0 ? (cellX - px) / dx
                : float.PositiveInfinity;
            float tMaxY = stepY > 0 ? (cellY + 1 - py) / dy
                : stepY < 0 ? (cellY - py) / dy
                : float.PositiveInfinity;

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
                    normal = new Vector2(-Mathf.Sign(dx), 0f);
                }
                else
                {
                    t = tMaxY;
                    cellY += stepY;
                    tMaxY += tDeltaY;
                    normal = new Vector2(0f, -Mathf.Sign(dy));
                }

                if (t > maxT)
                {
                    // 还没走到墙就超出预算了，终点停在 maxT 处
                    result.HitPos = new Vector2Int(cellX, cellY);
                    result.Distance = maxT;
                    return result;
                }

                // 走出网格：贴边结束，不算撞墙
                if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height)
                {
                    result.HitPos = new Vector2Int(Mathf.Clamp(cellX, 0, width - 1), Mathf.Clamp(cellY, 0, height - 1));
                    result.Distance = t;
                    return result;
                }

                if (MapState[cellX, cellY] == TileState.TileWall)
                {
                    result.IsBlocked = true;
                    result.HitPos = new Vector2Int(cellX, cellY);
                    result.Distance = t;
                    result.HitNormal = normal;
                    return result;
                }

                lastT = t;
            }

            Debug.LogWarning("[GridManager]：rayHitGrid 触发了步进上限，请检查 maxT 或网格尺寸");
            result.HitPos = new Vector2Int(Mathf.Clamp(cellX, 0, width - 1), Mathf.Clamp(cellY, 0, height - 1));
            result.Distance = lastT;
            return result;
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
        /// 1. 网格坐标转物理世界坐标（返回格子中心点）
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            Vector3 origin = GetGridOrigin();
            float x = origin.x + (gridPos.x + 0.5f) * _cellWidth;
            float z = origin.z + (gridPos.y + 0.5f) * _cellHeight;
            return new Vector3(x, _proxyObject.position.y, z);
        }

        /// <summary>
        /// 2. 物理世界坐标转网格坐标
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 origin = GetGridOrigin();
            int x = Mathf.FloorToInt((worldPos.x - origin.x) / _cellWidth);
            int y = Mathf.FloorToInt((worldPos.z - origin.z) / _cellHeight);

            // 限制在网格边界内
            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            return new Vector2Int(x, y);
        }


    }
}