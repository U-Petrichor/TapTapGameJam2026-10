using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockPanelGraphic : MaskableGraphic
    {
        [SerializeField] private Color _fillColor = Color.white;
        [SerializeField] private Color _borderColor = Color.black;
        [SerializeField, Min(0f)] private float _cornerRadius = 24f;
        [SerializeField, Min(0f)] private float _borderWidth = 5f;

        private const int CornerSegments = 8;

        public void Configure(Color fill, Color border, float radius, float borderWidth)
        {
            _fillColor = fill;
            _borderColor = border;
            _cornerRadius = Mathf.Max(0f, radius);
            _borderWidth = Mathf.Max(0f, borderWidth);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float radius = Mathf.Min(_cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2[] outer = RoundedRectangle(rect, radius);
            AddFilledPolygon(vh, outer, _fillColor);

            float border = Mathf.Min(_borderWidth, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (border <= 0f || _borderColor.a <= 0f) return;

            Rect innerRect = new Rect(rect.xMin + border, rect.yMin + border,
                rect.width - border * 2f, rect.height - border * 2f);
            Vector2[] inner = RoundedRectangle(innerRect, Mathf.Max(0f, radius - border));
            AddBorder(vh, outer, inner, _borderColor);
        }

        private static Vector2[] RoundedRectangle(Rect rect, float radius)
        {
            var points = new Vector2[(CornerSegments + 1) * 4];
            int index = 0;
            AddCorner(points, ref index, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f);
            AddCorner(points, ref index, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f);
            AddCorner(points, ref index, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f);
            AddCorner(points, ref index, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f);
            return points;
        }

        private static void AddCorner(Vector2[] points, ref int index, Vector2 center, float radius, float startAngle)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float angle = (startAngle + i * 90f / CornerSegments) * Mathf.Deg2Rad;
                points[index++] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }

        private static void AddFilledPolygon(VertexHelper vh, Vector2[] points, Color color)
        {
            int center = vh.currentVertCount;
            Vector2 average = Vector2.zero;
            foreach (var point in points) average += point;
            average /= points.Length;
            vh.AddVert(average, color, Vector2.zero);
            int start = vh.currentVertCount;
            foreach (var point in points) vh.AddVert(point, color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                vh.AddTriangle(center, start + i, start + next);
            }
        }

        private static void AddBorder(VertexHelper vh, Vector2[] outer, Vector2[] inner, Color color)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < outer.Length; i++)
            {
                vh.AddVert(outer[i], color, Vector2.zero);
                vh.AddVert(inner[i], color, Vector2.zero);
            }
            for (int i = 0; i < outer.Length; i++)
            {
                int next = (i + 1) % outer.Length;
                int current = start + i * 2;
                int nextPair = start + next * 2;
                vh.AddTriangle(current, nextPair, current + 1);
                vh.AddTriangle(current + 1, nextPair, nextPair + 1);
            }
        }
    }
}
