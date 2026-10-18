using System;
using UnityEngine;

namespace TapTapGameJam.UI
{
    // 只组合指定的美术污渍；局部随机源不会改变任务或股价的随机序列。
    public static class StainPattern
    {
        public static Color32[] Compose(Color32[] source, int sourceWidth, int sourceHeight,
            int width, int height, int seed)
        {
            if (source == null || source.Length != sourceWidth * sourceHeight
                || sourceWidth <= 0 || sourceHeight <= 0 || width <= 0 || height <= 0)
                throw new ArgumentException("Invalid stain source or output size.");
            var output = new Color32[width * height];
            var random = new System.Random(seed);
            // 四条主体污痕混合四处碎片，避免每个区域都出现相同的完整剪影。
            int[] cells = { 0, 2, 3, 5, 1, 4, 2, 3 };
            for (int stamp = 0; stamp < 8; stamp++)
            {
                bool fragment = stamp >= 4;
                int cell = cells[stamp];
                float cx = (cell % 3 + Range(random, 0.3f, 0.7f)) / 3f * width;
                float cy = (cell / 3 + Range(random, 0.3f, 0.7f)) / 2f * height;
                float stampWidth = width * Range(random, fragment ? 0.13f : 0.21f, fragment ? 0.25f : 0.32f);
                float stampHeight = height * Range(random, fragment ? 0.14f : 0.18f, fragment ? 0.28f : 0.32f);
                float angle = Range(random, -Mathf.PI, Mathf.PI);
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                float flip = random.Next(2) == 0 ? -1f : 1f;
                float cropWidth = fragment ? Range(random, 0.40f, 0.65f) : 1f;
                float cropLeft = fragment ? Range(random, 0.12f, 0.85f - cropWidth) : 0f;
                float phase = Range(random, 0f, Mathf.PI * 2f);
                float tint = Range(random, 0.88f, 1.12f);
                float opacity = Range(random, 0.78f, 1f);
                float extentX = Mathf.Abs(cos) * stampWidth + Mathf.Abs(sin) * stampHeight;
                float extentY = Mathf.Abs(sin) * stampWidth + Mathf.Abs(cos) * stampHeight;
                int xMin = Mathf.Max(0, Mathf.FloorToInt(cx - extentX * 0.5f));
                int xMax = Mathf.Min(width - 1, Mathf.CeilToInt(cx + extentX * 0.5f));
                int yMin = Mathf.Max(0, Mathf.FloorToInt(cy - extentY * 0.5f));
                int yMax = Mathf.Min(height - 1, Mathf.CeilToInt(cy + extentY * 0.5f));
                for (int y = yMin; y <= yMax; y++)
                    for (int x = xMin; x <= xMax; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float u = (cos * dx + sin * dy) / stampWidth * flip + 0.5f;
                        float v = (-sin * dx + cos * dy) / stampHeight + 0.5f;
                        if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                        // 小幅弯曲 UV，打破原图外轮廓与亮点的固定对应关系。
                        v += Mathf.Sin(u * 7f + phase) * 0.09f + Mathf.Sin(u * 15f + phase) * 0.02f;
                        if (v < 0f || v > 1f) continue;
                        float sourceU = cropLeft + u * cropWidth;
                        Color32 sample = Sample(source, sourceWidth, sourceHeight, sourceU, v);
                        // 裁片边缘淡出，避免出现矩形切口；微弱透明像素不参与完成计数。
                        float nx = (u - 0.5f) * 2f, ny = (v - 0.5f) * 2f;
                        float edge = fragment ? Mathf.Clamp01((1f - nx * nx - ny * ny
                            + Mathf.Sin(u * 17f + phase) * 0.1f) * 5f) : 1f;
                        float alpha = sample.a / 255f * opacity * edge;
                        if (alpha < 0.07f) continue;
                        int index = y * width + x;
                        Color32 below = output[index];
                        float belowAlpha = below.a / 255f;
                        float combined = alpha + belowAlpha * (1f - alpha);
                        float weight = belowAlpha * (1f - alpha);
                        output[index] = new Color32(
                            Byte((sample.r * tint * alpha + below.r * weight) / combined),
                            Byte((sample.g * tint * alpha + below.g * weight) / combined),
                            Byte((sample.b * tint * alpha + below.b * weight) / combined),
                            Byte(combined * 255f));
                    }
            }
            return output;
        }

        private static Color32 Sample(Color32[] source, int width, int height, float u, float v)
        {
            float x = u * (width - 1), y = v * (height - 1);
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, width - 1), y1 = Mathf.Min(y0 + 1, height - 1);
            Color a = Color.Lerp(source[y0 * width + x0], source[y0 * width + x1], x - x0);
            Color b = Color.Lerp(source[y1 * width + x0], source[y1 * width + x1], x - x0);
            return Color.Lerp(a, b, y - y0);
        }

        private static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
