using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StockHourglassSand : MaskableGraphic
    {
        [SerializeField] private Color _sandColor = new Color(0.96f, 0.63f, 0.16f, 1f);
        private float _progress;

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

            float centerX = rect.center.x;
            float topY = rect.yMax - rect.height * 0.16f;
            float bottomY = rect.yMin + rect.height * 0.16f;
            float neckY = rect.center.y;
            float chamberWidth = Mathf.Min(rect.width * 0.24f, rect.height * 0.18f);
            float neckWidth = Mathf.Max(1.5f, chamberWidth * 0.05f);

            float upperAmount = 1f - _progress;
            float upperSurface = Mathf.Lerp(neckY + 2f, topY, upperAmount);
            float upperWidth = Mathf.Lerp(neckWidth, chamberWidth, upperAmount);
            AddPolygon(vh, new[]
            {
                new Vector2(centerX - upperWidth, upperSurface),
                new Vector2(centerX + upperWidth, upperSurface),
                new Vector2(centerX, neckY)
            }, _sandColor);

            float lowerSurface = Mathf.Lerp(bottomY, neckY - 2f, _progress);
            float lowerSurfaceWidth = Mathf.Lerp(0f, neckWidth, _progress);
            float lowerBaseWidth = chamberWidth * _progress;
            AddPolygon(vh, new[]
            {
                new Vector2(centerX - lowerSurfaceWidth, lowerSurface),
                new Vector2(centerX + lowerSurfaceWidth, lowerSurface),
                new Vector2(centerX + lowerBaseWidth, bottomY),
                new Vector2(centerX - lowerBaseWidth, bottomY)
            }, _sandColor);

            if (_progress > 0f && _progress < 1f)
                AddTaperedStream(vh, centerX, neckY - rect.height * 0.12f, neckY + 1f, _sandColor);
        }

        private static void AddPolygon(VertexHelper vh, Vector2[] points, Color color)
        {
            int start = vh.currentVertCount;
            foreach (var point in points) vh.AddVert(point, color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }

        private static void AddTaperedStream(VertexHelper vh, float centerX, float bottom, float top, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(centerX - 2f, bottom), color, Vector2.zero);
            vh.AddVert(new Vector2(centerX - 0.8f, top), color, Vector2.zero);
            vh.AddVert(new Vector2(centerX + 0.8f, top), color, Vector2.zero);
            vh.AddVert(new Vector2(centerX + 2f, bottom), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
