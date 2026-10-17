using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 网格上的"到某个出发格要走几步"的距离场，以及沿距离场往回走的寻路。
    ///
    /// 这是"绕过障碍回到某个点"这类需求的公共实现：
    /// 巢穴用它让蟑螂归巢（<see cref="TapTapGameJam.Cockroach.CockNest.RefreshDistanceField"/>），
    /// 守卫用它从区域外走回出生点（<see cref="TapTapGameJam.Cockroach.Guard"/>）。
    /// 每个需要它的实体建一份自己的场：场是"从某个起点出发"的，不能共用。
    ///
    /// 为什么是"格数场 + 逐格下降"而不是 A*：
    /// 起点固定、终点很多（每只蟑螂 / 每个守卫每帧都要一个方向），
    /// 一次 BFS 把所有格子的代价都算出来，之后每帧只查几个格子，最省。
    /// </summary>
    public static class GridDistanceField
    {
        /// <summary>
        /// 不可达哨兵值。取一个足够大的 2 的幂，加 1 也不会溢出成负数，
        /// 与任何真实格数相比都大得离谱（网格边长不可能到百万格）。
        /// </summary>
        public const int Unreachable = 1 << 24;

        /// <summary>
        /// 建场用 4 邻域：对角步在贴墙的走廊里会"切墙角"，蟑螂和守卫都会卡进去。
        /// 只建场不改变实体自己怎么走，所以这里保守一点。
        /// </summary>
        private static readonly Vector2Int[] Directions4 =
        {
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        /// <summary>沿场下降用 8 邻域：已经确认两格都空，斜着走更贴自然路线</summary>
        private static readonly Vector2Int[] Directions8 =
        {
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        /// <summary>
        /// 从 <paramref name="origin"/> 出发铺一张全图距离场。
        /// 墙格和地图外永远保持 <see cref="Unreachable"/>，不会被走穿。
        /// </summary>
        public static int[,] Build(GridManager grid, Vector2Int origin)
        {
            int[,] field = new int[grid.width, grid.height];
            for (int x = 0; x < grid.width; x++)
            {
                for (int y = 0; y < grid.height; y++)
                {
                    field[x, y] = Unreachable;
                }
            }

            // 起点自己算 0 步。即使它落在墙里也照写：
            // 这是"从这里出发"的语义，由调用方负责把起点放在能站人的格子上。
            field[origin.x, origin.y] = 0;

            Queue<Vector2Int> open = new Queue<Vector2Int>();
            open.Enqueue(origin);

            while (open.Count > 0)
            {
                Vector2Int cell = open.Dequeue();
                int nextDistance = field[cell.x, cell.y] + 1;

                foreach (Vector2Int direction in Directions4)
                {
                    Vector2Int neighbor = cell + direction;

                    // IsBlocked 对越界返回 true，所以下面取下标一定在范围内
                    if (grid.IsBlocked(neighbor) || field[neighbor.x, neighbor.y] <= nextDistance)
                    {
                        continue;
                    }

                    field[neighbor.x, neighbor.y] = nextDistance;
                    open.Enqueue(neighbor);
                }
            }

            return field;
        }

        /// <summary>
        /// 沿距离场往起点走 <paramref name="steps"/> 格，返回"这一帧该看向哪"的世界点。
        /// 提前走不动（已经在起点格、或者周围全是更远的格子）就返回 <paramref name="goal"/>，
        /// 让调用方退化成朝目标直线走——墙和卡位由调用方自己的避障去处理。
        /// </summary>
        public static Vector2 Descend(GridManager grid, int[,] field, Vector2 from, Vector2 goal, int steps = 3)
        {
            Vector2Int cell = grid.WorldToGrid(from);

            // 已经和目标同一格：直接看向真正的目标点，别被格子中心带偏
            if (cell == grid.WorldToGrid(goal))
            {
                return goal;
            }

            for (int i = 0; i < steps; i++)
            {
                if (!TryStepDown(grid, field, cell, out Vector2Int next))
                {
                    break;
                }

                cell = next;
            }

            return grid.GridToWorld(cell);
        }

        /// <summary>找下一个"更近、且不会切墙角"的格子</summary>
        private static bool TryStepDown(GridManager grid, int[,] field, Vector2Int from, out Vector2Int next)
        {
            next = from;
            int best = field[from.x, from.y];

            foreach (Vector2Int direction in Directions8)
            {
                Vector2Int candidate = from + direction;

                if (grid.IsBlocked(candidate))
                {
                    continue;
                }

                // 斜着走必须两个正交邻居都空，否则就是贴着墙角斜穿
                if (direction.x != 0 && direction.y != 0)
                {
                    if (grid.IsBlocked(new Vector2Int(from.x + direction.x, from.y)) ||
                        grid.IsBlocked(new Vector2Int(from.x, from.y + direction.y)))
                    {
                        continue;
                    }
                }

                int distance = field[candidate.x, candidate.y];
                if (distance >= best)
                {
                    continue;
                }

                best = distance;
                next = candidate;
            }

            return next != from;
        }
    }
}
