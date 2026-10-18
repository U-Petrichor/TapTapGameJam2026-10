using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockHourglassSand : MaskableGraphic
    {
        [SerializeField] private Color _sandColor = new Color(0.96f, 0.63f, 0.16f, 1f);
        private float _progress;

        // 沙漏.png 的内壁轮廓，使用原图 295×314 的像素坐标（Y 向下）。
        // 沙面只裁切内腔，不缩窄整个沙堆，因而在任何进度都贴合直壁和斜壁。
        private static readonly Vector2[] UpperChamber =
        {
            new Vector2(86f, 40f), new Vector2(208f, 40f),
            new Vector2(208f, 82f), new Vector2(206f, 87f),
            new Vector2(147f, 140f), new Vector2(88f, 87f), new Vector2(86f, 82f)
        };
        private static readonly Vector2[] LowerChamber =
        {
            new Vector2(147f, 174f), new Vector2(208f, 232f),
            new Vector2(208f, 274f), new Vector2(86f, 274f), new Vector2(86f, 232f)
        };

        public void SetProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void ConfigureColor(Color color)
        {
            _sandColor = color;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // 和外框 Image.preserveAspect 使用同一个等比缩放区域。
            float scale = Mathf.Min(rect.width / 295f, rect.height / 314f);
            Vector2 origin = rect.center + new Vector2(-147.5f, 157f) * scale;
            if (_progress < 1f)
                AddChamber(vh, UpperChamber, Mathf.Lerp(40f, 140f, _progress), origin, scale);
            float lowerSurface = Mathf.Lerp(274f, 174f, _progress);
            if (_progress > 0f)
                AddChamber(vh, LowerChamber, lowerSurface, origin, scale);

            // 颈部的黑色框线不被沙流覆盖；可见沙流从下腔入口落到沙面。
            if (_progress > 0f && _progress < 1f)
                AddPolygon(vh, new[]
                {
                    new Vector2(146.7f, 174f), new Vector2(147.3f, 174f),
                    new Vector2(147.7f, lowerSurface), new Vector2(146.3f, lowerSurface)
                }, origin, scale);
        }

        private void AddChamber(VertexHelper vh, Vector2[] contour, float surface,
            Vector2 origin, float scale)
        {
            var clipped = new List<Vector2>(contour.Length + 2);
            Vector2 previous = contour[contour.Length - 1];
            foreach (Vector2 current in contour)
            {
                bool previousInside = previous.y >= surface;
                bool currentInside = current.y >= surface;
                if (previousInside != currentInside)
                {
                    float t = (surface - previous.y) / (current.y - previous.y);
                    clipped.Add(Vector2.Lerp(previous, current, t));
                }
                if (currentInside) clipped.Add(current);
                previous = current;
            }
            AddPolygon(vh, clipped, origin, scale);
        }

        private void AddPolygon(VertexHelper vh, IList<Vector2> points, Vector2 origin, float scale)
        {
            if (points.Count < 3) return;
            int start = vh.currentVertCount;
            foreach (Vector2 point in points)
                vh.AddVert(origin + new Vector2(point.x, -point.y) * scale, _sandColor, Vector2.zero);
            for (int i = 1; i < points.Count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
