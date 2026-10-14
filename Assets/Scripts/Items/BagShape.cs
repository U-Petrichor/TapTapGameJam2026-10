using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 背包中物品占据的格子形状（类似俄罗斯方块）。
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
    ///
    /// 【数据模型】
    /// - <see cref="initialOffsets"/> 是配置时写死的<b>初始形状，永不修改</b>；
    /// - <see cref="RotationSteps"/> 只记"顺时针转过几次 90 度"；
    /// - 要用的永远是通过 <see cref="Offsets"/> 拿到的当前形状，别直接读 initialOffsets。
    /// 这样旋转不会污染配置数据，转 4 次也能精确回到原样。
    /// </summary>
    [System.Serializable]
    public class BagShape
    {
        [Tooltip("初始形状的格子偏移。(0,0) 是鼠标咬住的抓取格，其余格相对它配置，可以是负数")]
        public List<Vector2Int> initialOffsets = new List<Vector2Int> { Vector2Int.zero };

        [Tooltip("在背包里显示用的格子框颜色")]
        public Color colorInGrid = Color.green;

        [SerializeField, Range(0, 3)] private int _rotationSteps;

        /// <summary>
        /// 当前朝向的偏移缓存。**刻意不序列化**：它完全是由 initialOffsets + RotationSteps 推出来的，
        /// 存下来只会让"改了 initialOffsets 但缓存还是旧的"这种脏数据被一起存进预制体。
        /// </summary>
        [System.NonSerialized] private List<Vector2Int> _offsets;

        /// <summary>顺时针旋转了 90 度的次数（0~3）</summary>
        public int RotationSteps
        {
            get => _rotationSteps;
            set
            {
                int normalized = ((value % 4) + 4) % 4;
                if (normalized == _rotationSteps)
                {
                    return;
                }

                _rotationSteps = normalized;
                RebuildOffsets();
            }
        }

        /// <summary>
        /// 当前朝向的占格偏移（已按 RotationSteps 旋转好，可直接用）。
        /// 缓存为空时会自动从 <see cref="initialOffsets"/> 重建，所以永远不会返回"空的旧缓存"。
        /// </summary>
        public List<Vector2Int> Offsets
        {
            get
            {
                if (_offsets == null || _offsets.Count == 0)
                {
                    RebuildOffsets();
                }

                return _offsets;
            }
        }

        /// <summary>占格数量（旋转不改变数量，所以直接看缓存）</summary>
        public int Count => Offsets.Count;

        /// <summary>当前朝向的外接矩形尺寸（宽 = 列跨度，高 = 行跨度）</summary>
        public Vector2Int Size
        {
            get
            {
                GetBounds(Offsets, out Vector2Int min, out Vector2Int max);
                return max - min + Vector2Int.one;
            }
        }

        public BagShape() { }

        public BagShape(IEnumerable<Vector2Int> source)
        {
            initialOffsets = new List<Vector2Int>(source ?? new List<Vector2Int> { Vector2Int.zero });
            _offsets = null;
        }

        /// <summary>顺时针旋转 90 度（绕抓取格转，(x,y) -&gt; (y,-x)）</summary>
        public void RotateClockwise()
        {
            RotationSteps += 1;
        }

        /// <summary>把形状按"抓取格放在 origin"算出来的实际占格</summary>
        public IEnumerable<Vector2Int> CellsOf(Vector2Int origin)
        {
            List<Vector2Int> offsets = Offsets;
            for (int i = 0; i < offsets.Count; i++)
            {
                yield return origin + offsets[i];
            }
        }

        /// <summary>当前朝向的格子偏移的外接矩形范围</summary>
        public static void GetBounds(List<Vector2Int> offsets, out Vector2Int min, out Vector2Int max)
        {
            if (offsets == null || offsets.Count == 0)
            {
                min = Vector2Int.zero;
                max = Vector2Int.zero;
                return;
            }

            min = offsets[0];
            max = offsets[0];
            for (int i = 1; i < offsets.Count; i++)
            {
                min = Vector2Int.Min(min, offsets[i]);
                max = Vector2Int.Max(max, offsets[i]);
            }
        }

        /// <summary>
        /// 按当前的 RotationSteps 重算缓存。
        /// 每一次旋转都是把上一次的结果整体做 (x,y) -&gt; (y,-x)，转 4 次精确回到初始形状。
        /// </summary>
        private void RebuildOffsets()
        {
            _offsets = Sanitize(initialOffsets);

            for (int step = 0; step < _rotationSteps; step++)
            {
                for (int i = 0; i < _offsets.Count; i++)
                {
                    Vector2Int cell = _offsets[i];
                    _offsets[i] = new Vector2Int(cell.y, -cell.x);
                }
            }

            RemoveDuplicates(_offsets);
        }

        private void OnValidate()
        {
            // 在 Inspector 里改了 initialOffsets 之后，缓存必须作废，否则会一直用旧形状
            _offsets = null;
            _rotationSteps = ((_rotationSteps % 4) + 4) % 4;
        }

        /// <summary>把配置里可能的空列表/重复格收敛成一份干净的偏移表</summary>
        private static List<Vector2Int> Sanitize(List<Vector2Int> source)
        {
            List<Vector2Int> result = source == null
                ? new List<Vector2Int>()
                : new List<Vector2Int>(source);

            if (result.Count == 0)
            {
                result.Add(Vector2Int.zero);
            }

            RemoveDuplicates(result);
            return result;
        }

        private static void RemoveDuplicates(List<Vector2Int> offsets)
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
