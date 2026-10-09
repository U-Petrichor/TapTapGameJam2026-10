using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 点线版：开局只显示一个开盘点，报价确认时再增加一个点并连接一条线。
    // 达到 13 个点后通过横向压缩继续把所有历史点保留在图中，不滚出左侧。
    [RequireComponent(typeof(CanvasRenderer), typeof(RectMask2D))]
    public class StockPointChart : MaskableGraphic
    {
        protected StockPointChart() => useLegacyMeshGeneration = false;

        [SerializeField] private TMP_FontAsset _priceFont;
        [SerializeField] private StockAxisSettings _axisSettings = new();

        private IReadOnlyList<float> _prices;
        private IReadOnlyList<float> _curvePrices;
        private StockAxisRange _axis;
        private StockPointPlotGraphic _plot;
        private readonly List<TMP_Text> _axisLabels = new();
        private int _count;
        private double _elapsed;
        private double _lastElapsed;
        private bool _frozen;

        public double AxisMinimum => _axis?.Minimum ?? 0;
        public double AxisMaximum => _axis?.Maximum ?? 1;
        public double DisplayedPrice => _count == 0 ? 0 : _prices[_count - 1];
        public int PriceCount => _count;
        public bool HasTarget => !_frozen && _curvePrices != null && _curvePrices.Count > _count;
        public double TargetPrice => HasTarget ? _curvePrices[_count] : DisplayedPrice;
        public double HorizontalPosition(double index)
        {
            // 13 个点以内预留 12 个间隔；超过后逐渐压缩，使全部点和唯一待吐点都在图内。
            double intervals = Math.Max(StockRound.VisiblePoints, Math.Max(1, _count));
            return Math.Max(0, index / intervals);
        }

        public void SetPriceFont(TMP_FontAsset font)
        {
            _priceFont = font;
            RefreshLabels();
        }

        public void SetPrices(IReadOnlyList<float> prices, double elapsedSeconds = 0,
            IReadOnlyList<float> curvePrices = null, bool frozen = false)
        {
            int count = prices?.Count ?? 0;
            bool reset = !ReferenceEquals(_prices, prices) || count < _count;
            _prices = prices;
            _curvePrices = curvePrices ?? prices;
            _count = count;
            _frozen = frozen;

            if (_count == 0)
            {
                _elapsed = _lastElapsed = 0;
                _axis = new StockAxisRange(_axisSettings);
                _axis.Reset(0, 0, 0);
                EnsureLayers();
                RefreshLabels();
                if (_plot != null) _plot.SetVerticesDirty();
                SetVerticesDirty();
                return;
            }

            _elapsed = double.IsNaN(elapsedSeconds)
                ? 0 : Math.Max(0, Math.Min(StockRound.Duration, elapsedSeconds));
            double seconds = reset ? 0 : Math.Max(0, _elapsed - _lastElapsed);
            _lastElapsed = _elapsed;

            double low = _prices[0], high = _prices[0];
            for (int i = 1; i < _count; i++)
            {
                low = Math.Min(low, _prices[i]);
                high = Math.Max(high, _prices[i]);
            }

            _axis ??= new StockAxisRange(_axisSettings);
            if (reset) _axis.Reset(low, high, DisplayedPrice);
            else if (!_frozen || seconds > 0) _axis.Step(low, high, DisplayedPrice, seconds);

            EnsureLayers();
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
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _plot = go.AddComponent<StockPointPlotGraphic>();
                _plot.Bind(this);
            }
        }

        private RectTransform CreateViewport(string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = min;
            rect.offsetMax = max;
            return rect;
        }

        private TMP_Text CreateLabel(Transform parent, string name, Vector2 size, Vector2 pivot,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.alignment = alignment;
            label.fontSize = 16;
            label.enableAutoSizing = true;
            label.fontSizeMin = 9;
            label.fontSizeMax = 16;
            label.color = new Color(0.10f, 0.17f, 0.24f);
            label.raycastTarget = false;
            return label;
        }

        private void RefreshLabels()
        {
            if (_axisLabels == null || _viewIsUnavailable()) return;
            if (_count > 0)
            {
                while (_axisLabels.Count < 5)
                    _axisLabels.Add(CreateLabel(transform, $"AxisLabel_{_axisLabels.Count + 1}",
                        new Vector2(68, 24), new Vector2(1, 0.5f), TextAlignmentOptions.Right));
            }

            Rect r = rectTransform.rect;
            for (int i = 0; i < _axisLabels.Count; i++)
            {
                var label = _axisLabels[i];
                label.gameObject.SetActive(_count > 0);
                if (_count == 0) continue;
                label.font = _priceFont != null ? _priceFont : TMP_Settings.defaultFontAsset;
                label.text = (_axis.Minimum + (_axis.Maximum - _axis.Minimum) * i / 4).ToString("0.00");
                label.rectTransform.anchoredPosition = new Vector2(r.xMin + 68,
                    Mathf.Lerp(r.yMin + 52, r.yMax - 22, i / 4f)) - r.center;
            }

        }

        private bool _viewIsUnavailable() => _axis == null && _count == 0;

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
            // 新版只保留水平网格线，横向压缩由点的位置表达时间进展。
            for (int i = 0; i < 5; i++)
                Line(vh, new Vector2(left, Mathf.Lerp(bottom, top, i / 4f)),
                    new Vector2(right, Mathf.Lerp(bottom, top, i / 4f)), 1, grid);
        }

        internal void PopulatePrices(VertexHelper vh, Rect plotRect)
        {
            if (_count == 0) return;
            int last = _count - 1;
            for (int i = 0; i < last; i++)
            {
                Vector2 a = PlotPoint(plotRect, i, _prices[i]);
                Vector2 b = PlotPoint(plotRect, i + 1, _prices[i + 1]);
                Line(vh, a, b, 3, color);
            }

            for (int i = 0; i < _count; i++)
                Disc(vh, PlotPoint(plotRect, i, _prices[i]), i == last ? 5 : 3.5f, color);

        }

        private Vector2 PlotPoint(Rect rect, double index, double price) => new Vector2(
            Mathf.LerpUnclamped(rect.xMin, rect.xMax, (float)HorizontalPosition(index)),
            Mathf.LerpUnclamped(rect.yMin, rect.yMax, (float)VerticalPosition(price)));

        private double VerticalPosition(double price) => (_axis == null || _axis.Maximum <= _axis.Minimum)
            ? 0.5 : (price - _axis.Minimum) / (_axis.Maximum - _axis.Minimum);

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, tint, Vector2.zero);
            vh.AddVert(a + normal, tint, Vector2.zero);
            vh.AddVert(b + normal, tint, Vector2.zero);
            vh.AddVert(b - normal, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
