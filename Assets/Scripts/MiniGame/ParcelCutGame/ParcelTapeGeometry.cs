using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    // For these six unbranched, elongated tape images. Alpha rows are bottom-up,
    // matching Texture2D.GetPixels32 and UI UVs. The result is baked into the scene.
    public sealed class ParcelTapeGeometry
    {
        public Vector2[] Path { get; }
        public Vector2[] Outline { get; }

        private ParcelTapeGeometry(Vector2[] path, Vector2[] outline)
        {
            Path = path;
            Outline = outline;
        }

        public static ParcelTapeGeometry Extract(int width, int height, byte[] alpha)
        {
            if (width < 2 || height < 2 || alpha == null || alpha.Length != width * height)
                throw new ArgumentException("胶带透明度数据与图片尺寸不匹配。");
            bool horizontal = width >= height;
            int length = horizontal ? width : height;
            int crossLength = horizontal ? height : width;
            var centers = new List<Vector2>();
            var lower = new List<Vector2>();
            var upper = new List<Vector2>();
            // Keep the silhouette's actual edges, including its change in width.
            for (int along = 0; along < length; along++)
            {
                int min = crossLength, max = -1;
                for (int cross = 0; cross < crossLength; cross++)
                {
                    int x = horizontal ? along : cross;
                    int y = horizontal ? cross : along;
                    if (alpha[y * width + x] < 128) continue;
                    min = Math.Min(min, cross);
                    max = Math.Max(max, cross);
                }
                if (max < min) continue;
                float axis = (along + 0.5f) / length;
                float low = min / (float)crossLength;
                float high = (max + 1f) / crossLength;
                centers.Add(horizontal ? new Vector2(axis, (low + high) * 0.5f) : new Vector2((low + high) * 0.5f, axis));
                lower.Add(horizontal ? new Vector2(axis, low) : new Vector2(low, axis));
                upper.Add(horizontal ? new Vector2(axis, high) : new Vector2(high, axis));
            }
            if (centers.Count < 2) throw new ArgumentException("胶带图片没有足够的不透明像素。");

            // Reduce scene data without fitting a guessed straight line through it.
            int count = Math.Min(64, centers.Count);
            var path = new Vector2[count];
            var outline = new Vector2[count * 2];
            for (int i = 0; i < count; i++)
            {
                int sample = (int)Math.Round(i * (centers.Count - 1.0) / (count - 1));
                path[i] = centers[sample];
                outline[i] = lower[sample];
                outline[count * 2 - 1 - i] = upper[sample];
            }
            return new ParcelTapeGeometry(path, outline);
        }

        public static bool Contains(IReadOnlyList<Vector2> outline, Vector2 point)
        {
            if (outline == null || outline.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                Vector2 a = outline[i], b = outline[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
