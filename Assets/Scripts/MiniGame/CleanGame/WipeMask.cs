using System;

namespace TapTapGameJam.UI
{
    // 统计原始非透明污渍像素；擦除同一像素只计一次。
    public sealed class WipeMask
    {
        private readonly bool[] _dirty;
        public int Width { get; }
        public int Height { get; }
        public int Total { get; }
        public int Remaining { get; private set; }
        public float Progress => Total == 0 ? 1f : 1f - (float)Remaining / Total;
        public bool IsDirty(int index) => _dirty[index];

        public WipeMask(int width, int height, byte[] alpha)
        {
            if (width <= 0 || height <= 0 || alpha == null || alpha.Length != checked(width * height))
                throw new ArgumentException("Invalid stain mask.");
            Width = width;
            Height = height;
            _dirty = new bool[alpha.Length];
            int total = 0;
            for (int i = 0; i < alpha.Length; i++) { _dirty[i] = alpha[i] > 0; if (_dirty[i]) total++; }
            Total = Remaining = total;
        }

        public bool EraseSegment(float ax, float ay, float bx, float by, float radius)
        {
            if (radius <= 0 || !Finite(ax) || !Finite(ay) || !Finite(bx) || !Finite(by) || !Finite(radius)) return false;
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - radius));
            int maxX = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(ax, bx) + radius));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - radius));
            int maxY = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(ay, by) + radius));
            float dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            bool changed = false;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * Width + x;
                    if (!_dirty[index]) continue;
                    float t = length > 0 ? Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (y - ay) * dy) / length)) : 0f;
                    float ex = x - (ax + dx * t), ey = y - (ay + dy * t);
                    if (ex * ex + ey * ey > radius * radius) continue;
                    _dirty[index] = false;
                    Remaining--;
                    changed = true;
                }
            return changed;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
