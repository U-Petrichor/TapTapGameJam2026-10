using System;

namespace TapTapGameJam.UI
{
    /// <summary>求从矩形中心沿目标方向射出的射线与边界的交点。</summary>
    public static class TaskIndicatorGeometry
    {
        public static bool TryGetEdgePosition(float directionX, float directionY,
            float halfWidth, float halfHeight, out float x, out float y)
        {
            x = y = 0f;
            if (halfWidth <= 0f || halfHeight <= 0f ||
                !IsFinite(halfWidth) || !IsFinite(halfHeight) ||
                !IsFinite(directionX) || !IsFinite(directionY)) return false;

            // 目标恰好位于正后方时，约定指向屏幕下边缘。
            if (Math.Abs(directionX) < 0.0001f && Math.Abs(directionY) < 0.0001f)
            {
                y = -halfHeight;
                return true;
            }
            float tx = Math.Abs(directionX) < 0.0001f
                ? float.PositiveInfinity : halfWidth / Math.Abs(directionX);
            float ty = Math.Abs(directionY) < 0.0001f
                ? float.PositiveInfinity : halfHeight / Math.Abs(directionY);
            float distance = Math.Min(tx, ty);
            x = directionX * distance;
            y = directionY * distance;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
