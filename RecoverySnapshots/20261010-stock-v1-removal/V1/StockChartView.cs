using System;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{
    public sealed class StockChartView
    {
        public const int TimeSlots = StockRound.VisiblePoints + 1;
        private IReadOnlyList<float> _prices, _curvePrices;
        private int _count;
        private double _lastTime;
        private bool _frozen;
        private readonly StockAxisRange _axis;
        public StockChartView(StockAxisSettings settings = null) { _axis = new StockAxisRange(settings); }
        public double DisplayStart { get; private set; }
        public double AxisMinimum => _axis.Minimum;
        public double AxisMaximum => _axis.Maximum;
        public double SegmentProgress { get; private set; }
        public double TipIndex => _count == 0 ? 0 : _count - 1 + SegmentProgress;
        public double TipPrice { get; private set; }
        public bool IsDrawing => SegmentProgress > 0;
        public bool HasTarget => !_frozen && _count < StockRound.MaxPoints && _curvePrices != null && _curvePrices.Count > _count;
        public int FirstLabelIndex => Math.Max(0, (int)Math.Floor(DisplayStart));
        public int FirstRenderedIndex => Math.Max(0, FirstLabelIndex - 1);
        public int PriceCount => _count;

        public void SetFrame(IReadOnlyList<float> prices, double elapsedSeconds,
            IReadOnlyList<float> curvePrices = null, bool frozen = false)
        {
            int count = prices?.Count ?? 0;
            bool reset = !ReferenceEquals(_prices, prices) || count < _count;
            _prices = prices; _curvePrices = curvePrices ?? prices;
            if (count == 0)
            {
                _count = 0; _lastTime = DisplayStart = SegmentProgress = TipPrice = 0;
                _frozen = false; _axis.Reset(0, 0, 0);
                return;
            }
            double time = double.IsNaN(elapsedSeconds) ? 0 : Math.Max(0, Math.Min(StockRound.TradingSeconds, elapsedSeconds));
            double seconds = reset ? 0 : Math.Max(0, time - _lastTime);
            _lastTime = time;
            _count = count;
            _frozen = frozen || count >= StockRound.MaxPoints;
            DisplayStart = time / StockRound.RefreshSeconds;
            SegmentProgress = count < StockRound.MaxPoints && _curvePrices.Count > count
                ? Math.Max(0, Math.Min(1, DisplayStart - (count - StockRound.VisiblePoints))) : 0;
            TipPrice = SegmentProgress > 0 ? StockCurve.Evaluate(_curvePrices, count - 1, SegmentProgress) : prices[count - 1];
            double low = TipPrice, high = TipPrice;
            for (int i = FirstLabelIndex; i < count; i++)
            { low = Math.Min(low, prices[i]); high = Math.Max(high, prices[i]); }
            if (reset) _axis.Reset(low, high, TipPrice);
            else if (!_frozen || seconds > 0) _axis.Step(low, high, TipPrice, seconds);
        }
        public double CurvePrice(int segment, double progress) => StockCurve.Evaluate(_curvePrices, segment, progress);
        public double HorizontalPosition(double index) => (index + 1 - DisplayStart) / TimeSlots;
        public double VerticalPosition(double price) => (price - AxisMinimum) / (AxisMaximum - AxisMinimum);
        public bool IsPointVisible(int index)
        {
            if (_prices == null || index < 0 || index >= _count) return false;
            double x = HorizontalPosition(index), y = VerticalPosition(_prices[index]);
            return x >= 0 && x <= 1 && y >= 0 && y <= 1;
        }
    }
}
