using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TapTapGameJam.UI
{
    // 十二个已完成报价与一个正在绘制的位置，后续报价只用于插值斜率。
    [RequireComponent(typeof(CanvasRenderer), typeof(RectMask2D))]
    public class StockChart : MaskableGraphic
    {
        protected StockChart() { useLegacyMeshGeneration = false; }
        private IReadOnlyList<float> _prices;
        [SerializeField] private TMP_FontAsset _priceFont;
        [SerializeField] private StockAxisSettings _axisSettings = new();
        private StockChartView _view;
        private readonly List<TMP_Text> _priceLabels = new();
        private readonly List<TMP_Text> _axisLabels = new();
        private StockPlotGraphic _plot;
        private RectTransform _labelViewport;
        private TMP_Text _upcomingLabel;
        private int _count;
        public float DisplayStart => (float)(_view?.DisplayStart ?? 0);
        public double AxisMinimum => _view?.AxisMinimum ?? 0;
        public double AxisMaximum => _view?.AxisMaximum ?? 1;
        public double DisplayedPrice => _view?.TipPrice ?? 0;
        public double DrawingProgress => _view?.SegmentProgress ?? 0;
        public int VisiblePriceLabelCount
        {
            get
            {
                int count = 0;
                if (_view == null) return 0;
                for (int i = _view.FirstLabelIndex; i < _count; i++)
                    if (_view.IsPointVisible(i)) count++;
                return count;
            }
        }
        public void SetPriceFont(TMP_FontAsset font) { _priceFont = font; RefreshLabels(); }
        public void SetPrices(IReadOnlyList<float> prices, double elapsedSeconds = 0,
            IReadOnlyList<float> curvePrices = null, bool frozen = false)
        {
            _prices = prices;
            _count = prices?.Count ?? 0;
            _view ??= new StockChartView(_axisSettings);
            _view.SetFrame(prices, elapsedSeconds, curvePrices, frozen);
            if (_count > 0) EnsureLayers();
            RefreshLabels();
            if (_plot != null) _plot.SetVerticesDirty();
            SetVerticesDirty();
        }
        private void EnsureLayers()
        {
            if (_plot == null)
            {
                var viewport = CreateViewport("PlotViewport", new Vector2(78, 52), new Vector2(-58, -22));
                var go = new GameObject("PricePlot", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(viewport, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _plot = go.AddComponent<StockPlotGraphic>();
                _plot.Bind(this);
            }
            if (_labelViewport == null)
                _labelViewport = CreateViewport("PriceLabelViewport", new Vector2(48, 0), new Vector2(-28, -22));
            if (_upcomingLabel == null)
                _upcomingLabel = CreateLabel(transform, "UpcomingLabel", new Vector2(90, 24),
                    new Vector2(0.5f, 1), TextAlignmentOptions.Center);
        }
        private RectTransform CreateViewport(string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = min; rect.offsetMax = max;
            return rect;
        }
        private Vector2 PointPosition(int index, float price)
        {
            Rect r = rectTransform.rect;
            return new Vector2(Mathf.LerpUnclamped(r.xMin + 78, r.xMax - 58,
                (float)_view.HorizontalPosition(index)),
                Mathf.LerpUnclamped(r.yMin + 52, r.yMax - 22, (float)_view.VerticalPosition(price)));
        }
        private TMP_Text CreateLabel(Transform parent, string name, Vector2 size, Vector2 pivot,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot; rect.sizeDelta = size;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.alignment = alignment;
            label.fontSize = 16;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9; label.fontSizeMax = 16;
            label.color = new Color(0.10f, 0.17f, 0.24f);
            label.raycastTarget = false;
            return label;
        }
        private void RefreshLabels()
        {
            if (_priceLabels == null || _axisLabels == null || _view == null) return;
            if (_count > 0 && _labelViewport != null)
            {
                while (_priceLabels.Count < StockRound.VisiblePoints)
                    _priceLabels.Add(CreateLabel(_labelViewport, $"PriceLabel_{_priceLabels.Count + 1}",
                        new Vector2(54, 26), new Vector2(0.5f, 1), TextAlignmentOptions.Center));
                while (_axisLabels.Count < 5)
                    _axisLabels.Add(CreateLabel(transform, $"AxisLabel_{_axisLabels.Count + 1}",
                        new Vector2(68, 24), new Vector2(1, 0.5f), TextAlignmentOptions.Right));
            }
            for (int i = 0; i < _priceLabels.Count; i++)
            {
                var label = _priceLabels[i];
                int index = _view.FirstLabelIndex + i;
                // 点仍在窗口内时完整显示价格；退出后隐藏标签，折线继续由遮罩裁剪。
                bool active = _view.IsPointVisible(index);
                label.gameObject.SetActive(active);
                if (!active) continue;
                label.font = _priceFont != null ? _priceFont : TMP_Settings.defaultFontAsset;
                label.text = _prices[index].ToString("0.00");
                label.fontStyle = index == _count - 1 ? FontStyles.Bold : FontStyles.Normal;
                var local = PointPosition(index, _prices[index]) + Vector2.down * 12;
                label.rectTransform.anchoredPosition = (Vector2)_labelViewport.InverseTransformPoint(
                    rectTransform.TransformPoint(local)) - _labelViewport.rect.center;
            }
            Rect r = rectTransform.rect;
            for (int i = 0; i < _axisLabels.Count; i++)
            {
                var label = _axisLabels[i];
                label.gameObject.SetActive(_count > 0);
                if (_count == 0) continue;
                label.font = _priceFont != null ? _priceFont : TMP_Settings.defaultFontAsset;
                label.text = (_view.AxisMinimum + (_view.AxisMaximum - _view.AxisMinimum) * i / 4).ToString("0.00");
                label.rectTransform.anchoredPosition = new Vector2(r.xMin + 68,
                    Mathf.Lerp(r.yMin + 52, r.yMax - 22, i / 4f)) - r.center;
            }
            if (_upcomingLabel != null)
            {
                _upcomingLabel.gameObject.SetActive(_count > 0 && _view.HasTarget);
                _upcomingLabel.font = _priceFont != null ? _priceFont : TMP_Settings.defaultFontAsset;
                _upcomingLabel.text = "绘制目标";
                _upcomingLabel.fontSizeMax = 14;
                _upcomingLabel.rectTransform.anchoredPosition = new Vector2(
                    PointPosition(_count, 0).x, r.yMin + 32) - r.center;
            }
        }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            RefreshLabels();
            if (_plot != null) _plot.SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float left = r.xMin + 78, right = r.xMax - 58, bottom = r.yMin + 52, top = r.yMax - 22;
            Color grid = new Color(0.72f, 0.79f, 0.87f, 0.45f);
            for (int i = 0; i < 5; i++)
                Line(vh, new Vector2(left, Mathf.Lerp(bottom, top, i / 4f)),
                    new Vector2(right, Mathf.Lerp(bottom, top, i / 4f)), 1, grid);
            for (int i = 0; i < StockChartView.TimeSlots; i++)
            {
                float x = Mathf.Lerp(left, right, (i + 1) / (float)StockChartView.TimeSlots);
                Line(vh, new Vector2(x, bottom), new Vector2(x, top), 1, grid);
            }
        }
        internal void PopulatePrices(VertexHelper vh, Rect plotRect)
        {
            if (_count == 0) return;
            int first = _view.FirstRenderedIndex;
            for (int segment = first; segment < _count; segment++)
            {
                double limit = segment < _count - 1 ? 1 : _view.SegmentProgress;
                if (limit <= 0) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt((float)limit * 16));
                Vector2 previous = PlotPoint(plotRect, segment, _prices[segment]);
                for (int step = 1; step <= steps; step++)
                {
                    double progress = limit * step / steps;
                    Vector2 point = PlotPoint(plotRect, segment + progress, _view.CurvePrice(segment, progress));
                    Line(vh, previous, point, 3, color);
                    previous = point;
                }
            }
            for (int i = first; i < _count; i++)
                Disc(vh, PlotPoint(plotRect, i, _prices[i]), i == _count - 1 ? 5 : 3.5f, color);
            if (_view.IsDrawing)
                Disc(vh, PlotPoint(plotRect, _view.TipIndex, _view.TipPrice), 4, color);
            // 目标的时间位置可以显示，报价标签只在这段曲线完成后出现。
            if (_view.HasTarget)
            {
                float upcoming = Mathf.LerpUnclamped(plotRect.xMin, plotRect.xMax,
                    (float)_view.HorizontalPosition(_count));
                var tint = new Color(color.r, color.g, color.b, 0.25f);
                for (float y = plotRect.yMin; y < plotRect.yMax; y += 14)
                    Line(vh, new Vector2(upcoming, y),
                        new Vector2(upcoming, Mathf.Min(y + 6, plotRect.yMax)), 1, tint);
            }
        }
        private Vector2 PlotPoint(Rect rect, double index, double price) => new Vector2(
            Mathf.LerpUnclamped(rect.xMin, rect.xMax, (float)_view.HorizontalPosition(index)),
            Mathf.LerpUnclamped(rect.yMin, rect.yMax, (float)_view.VerticalPosition(price)));
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, tint, Vector2.zero); vh.AddVert(a + normal, tint, Vector2.zero);
            vh.AddVert(b + normal, tint, Vector2.zero); vh.AddVert(b - normal, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
