using System.Collections.Generic;
using UnityEngine;
using TapTapGameJam.Core;

namespace TapTapGameJam.Cockroach
{
    
    /// <summary>
    /// 蟑螂巢穴。既是归巢目标，也是信息素归巢通道的起点。
    /// </summary>
    public class CockNest : MonoBehaviour
    {
        
        /// <summary>巢穴距离场里"不可达"的哨兵值</summary>
        public const int FarDistance = 1 << 24;

        public Vector2 WorldPosition => Cockroach.FlatOf(transform.position);

        [Tooltip("巢穴本体半径（世界单位）。蟑螂走到这个范围内就算到家")]
        public float radius = 0.6f;

        [Tooltip("活动范围半径（世界单位）。超出所有巢穴范围的蟑螂会被拉回来")]
        public float range = 10f;

        [Tooltip("是否已启用")]
        public bool isActive = false;

        public List<Cockroach> CockroachList { get; private set; } = new List<Cockroach>();

        private CockroachCommand currentCommand = CockroachCommand.Hatch;

        /// <summary>
        /// 到本巢穴的格数距离场
        /// </summary>
        public int[,] DistanceField {get; private set;}

        private void OnEnable()
        {
            // 和 PickableItem 一样由自己登记，CockroachManager 不主动遍历场景
            CockroachManager.Instance.CockNestList.Add(this);
            Debug.Log("Nest created.");
        }

        private void OnDisable()
        {
            if(CockroachManager.Instance)
                CockroachManager.Instance.CockNestList.Remove(this);
            // TODO: 移除List中所有蟑螂（删除实例）
        }

        // 不含生成/销毁语义
        public void Add(Cockroach cockroach)
        {
            CockroachList.Add(cockroach);
            
        }

        public void Remove(Cockroach cockroach)
        {
            CockroachList.Remove(cockroach);
        }
        
        /// <summary>
        /// 点击巢穴时调用的入口：切到下一条行动指令，然后派发。
        /// </summary>
        public void CycleCommand()
        {
            int count = System.Enum.GetValues(typeof(CockroachCommand)).Length;
            currentCommand = (CockroachCommand)(((int)currentCommand + 1) % count);

            DispatchCommand(currentCommand);
            Debug.Log("Nest command: " + currentCommand);
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
        /// </summary>
        public void RefreshDistanceField()
        {
            GridManager grid = GridManager.Instance;

            int width = grid.width;
            int height = grid.height;

            DistanceField = new int[width, height];

            for (int i = 0; i < width; i++)
                for (int j = 0; j <  height; j++)
                    DistanceField[i, j] = FarDistance;

            Vector2Int center = grid.WorldToGrid(WorldPosition);
            DistanceField[center.x, center.y] = 0;
            
            Vector2Int[] direction = new Vector2Int[4]
            {
                new Vector2Int(0, -1),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            q.Enqueue(center);
            while (q.Count > 0)
            {
                Vector2Int u = q.Dequeue();
                int disu = DistanceField[u.x, u.y];
                foreach (Vector2Int dir in direction)
                {
                    Vector2Int v = u + dir;
                    if (!grid.IsBlocked(v) && DistanceField[v.x, v.y] > disu + 1)
                    {
                        DistanceField[v.x, v.y] = disu + 1;
                        q.Enqueue(v);
                    }
                }
            }
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
