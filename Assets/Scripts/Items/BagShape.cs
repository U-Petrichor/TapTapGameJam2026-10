using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 背包中物品占据的格子形状（类似俄罗斯方块），旋转由自己负责。
    ///
    /// 【坐标约定，务必先读】
    /// - Vector2Int.x = 列，向右为正；Vector2Int.y = 行，<b>向下</b>为正。
    /// - <b>(0,0) = 抓取格</b>：玩家用鼠标拖动这件物品时，鼠标咬住的就是这一格。
    ///   配置形状时就按这个基准填其他格，所以偏移 <b>可以是负数</b>：
    ///   例如一个 3x3 的件，抓中心就是
    ///       (-1,-1) (0,-1) (1,-1)
    ///       (-1, 0) (0, 0) (1, 0)
    ///       (-1, 1) (0, 1) (1, 1)
    /// - 旋转是"绕抓取格转"：因为 (0,0) 在旋转下不动，所以转完之后鼠标依然咬在同一格上，
    ///   不需要任何补偿计算。
    /// - offsets 不做归一化，也不保证非负 —— 越界与否交给 PlayerBag.CanPlace 判。
    /// </summary>
    [System.Serializable]
    public class BagShape
    {
        [Tooltip("形状包含的格子偏移。(0,0) 是鼠标咬住的抓取格，其余格相对它配置，可以是负数")]
        public List<Vector2Int> offsets = new List<Vector2Int> { Vector2Int.zero };

        public BagShape() { }

        public BagShape(IEnumerable<Vector2Int> source)
        {
            offsets = CopyOf(source);
        }

        /// <summary>占格数量</summary>
        public int Count => offsets != null ? offsets.Count : 0;

        /// <summary>外接矩形尺寸（宽 = 列跨度，高 = 行跨度）。只看跨度，所以负偏移不影响结果</summary>
        public Vector2Int Size
        {
            get
            {
                if (offsets == null || offsets.Count == 0)
                {
                    return Vector2Int.one;
                }

                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;

                for (int i = 0; i < offsets.Count; i++)
                {
                    Vector2Int cell = offsets[i];
                    if (cell.x < minX) minX = cell.x;
                    if (cell.x > maxX) maxX = cell.x;
                    if (cell.y < minY) minY = cell.y;
                    if (cell.y > maxY) maxY = cell.y;
                }

                return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
            }
        }

        /// <summary>
        /// 原地顺时针旋转 90 度，直接改 offsets。
        ///
        /// 变换是 (x, y) -&gt; (y, -x)：这是"绕原点顺时针转"，原点 (0,0) 自己不动，
        /// 所以抓取格天然保持不动。连转 4 次能精确回到原样（t^4 = id），不是近似。
        /// </summary>
        public void RotateInPlace()
        {
            if (offsets == null || offsets.Count == 0)
            {
                offsets = new List<Vector2Int> { Vector2Int.zero };
                return;
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int cell = offsets[i];
                offsets[i] = new Vector2Int(cell.y, -cell.x);
            }

            // 去掉旋转可能造出来的重复格（配置里本来就有重复格时也会被顺手收掉）
            RemoveDuplicates();
        }

        /// <summary>把形状按"抓取格放在 origin"算出来的实际占格</summary>
        public IEnumerable<Vector2Int> CellsOf(Vector2Int origin)
        {
            if (offsets == null)
            {
                yield break;
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                yield return origin + offsets[i];
            }
        }

        /// <summary>拷贝一份可写 List（字段为空时返回单格形状，保证不是 null）</summary>
        public static List<Vector2Int> CopyOf(IEnumerable<Vector2Int> source)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            if (source == null)
            {
                result.Add(Vector2Int.zero);
                return result;
            }

            foreach (Vector2Int cell in source)
            {
                result.Add(cell);
            }

            if (result.Count == 0)
            {
                result.Add(Vector2Int.zero);
            }

            return result;
        }

        private void RemoveDuplicates()
        {
            for (int i = offsets.Count - 1; i > 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if (offsets[i] == offsets[j])
                    {
                        offsets.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}
