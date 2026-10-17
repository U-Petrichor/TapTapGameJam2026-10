using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>
    /// 守卫的行动区域：世界 XZ 平面上一块轴对齐的矩形，由外置 plane（search_range_plane）的
    /// 世界包围盒决定。plane 转没转、是 Plane 还是 Quad 都无所谓，这里只取它在 XZ 上的投影。
    ///
    /// 职责边界：只做几何（包含 / 夹紧 / 取点）和"从 plane 读出来"这一件事。
    /// 它不知道守卫、不知道网格、也不知道白天黑夜。
    /// </summary>
    public readonly struct GuardRegion
    {
        /// <summary>区域在 XZ 上的最小角（世界坐标）</summary>
        public readonly Vector2 Min;

        /// <summary>区域在 XZ 上的最大角（世界坐标）</summary>
        public readonly Vector2 Max;

        public GuardRegion(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        public Vector2 Size => Max - Min;

        public Vector2 Center => (Min + Max) * 0.5f;

        /// <summary>退化成一条线或一个点的区域没有意义</summary>
        public bool IsValid => Size.x > 1e-3f && Size.y > 1e-3f;

        /// <summary><paramref name="point"/> 在不在区域内（<paramref name="margin"/> 是内缩量）</summary>
        public bool Contains(Vector2 point, float margin = 0f)
        {
            return point.x >= Min.x - margin && point.x <= Max.x + margin
                   && point.y >= Min.y - margin && point.y <= Max.y + margin;
        }

        /// <summary>把点夹进区域</summary>
        public Vector2 Clamp(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, Min.x, Max.x),
                Mathf.Clamp(point.y, Min.y, Max.y));
        }

        /// <summary>
        /// 从 plane 的世界包围盒读出区域（只取 XZ，高度不参与）。
        /// 返回 false 表示这个物体上没有 Renderer，或者它在 XZ 上退化了。
        /// </summary>
        public static bool TryFromPlane(Transform plane, out GuardRegion region)
        {
            region = default;

            if (plane == null)
            {
                return false;
            }

            // 和 GridManager.TryGetFootprint 用同一套约定：bounds 已经包含了 mesh 尺寸、
            // pivot 偏移和 transform，所以 Plane / Quad、缩放、旋转都不用另外处理。
            Renderer renderer = plane.GetComponent<Renderer>();
            if (renderer == null)
            {
                return false;
            }

            Bounds bounds = renderer.bounds;
            region = new GuardRegion(
                new Vector2(bounds.min.x, bounds.min.z),
                new Vector2(bounds.max.x, bounds.max.z));

            return region.IsValid;
        }
    }
}
