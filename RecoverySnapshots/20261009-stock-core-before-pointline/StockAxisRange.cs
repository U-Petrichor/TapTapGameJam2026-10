using System;

namespace TapTapGameJam.UI
{
    [Serializable]
    public sealed class StockAxisSettings
    {
        public float SafetyMargin = 0.15f;
        public float ExpansionSeconds = 0.2f;
        public float StableSeconds = 2;
        public float ShrinkSeconds = 1;
        public float MinimumSpanRatio = 0.1f;
    }

    // 触发以目标范围判断，动画以显示范围推进；扩展期间不会因旧刻度每帧重启动画。
    public sealed class StockAxisRange
    {
        public const double MinimumSpan = 0.01;
        private readonly StockAxisSettings _settings;
        private double _targetMin, _targetMax, _startMin, _startMax, _animationTime, _animationSeconds;
        private double _stableTime;
        public double Minimum { get; private set; }
        public double Maximum { get; private set; } = MinimumSpan;
        public double TargetMinimum => _targetMin;
        public double TargetMaximum => _targetMax;
        public StockAxisRange(StockAxisSettings settings = null)
        {
            _settings = settings ?? new StockAxisSettings();
            if (!Valid(_settings.SafetyMargin, 0.01, 0.4)
                || !Valid(_settings.ExpansionSeconds, 0.01, 10)
                || !Valid(_settings.StableSeconds, 0, 60)
                || !Valid(_settings.ShrinkSeconds, 0.01, 30)
                || !Valid(_settings.MinimumSpanRatio, 0.001, 1))
                throw new ArgumentOutOfRangeException(nameof(settings));
        }
        private static bool Valid(float value, double min, double max) => !float.IsNaN(value) && value >= min && value <= max;
        private void Range(double low, double high, double tip, out double min, out double max)
        {
            double width = Math.Max(MinimumSpan, Math.Max(high - low, Math.Abs(tip) * _settings.MinimumSpanRatio))
                / (1 - 2 * _settings.SafetyMargin);
            double center = (low + high) * 0.5;
            min = Math.Max(0, center - width * 0.5);
            max = min + width;
        }
        public void Reset(double low, double high, double tip)
        {
            Range(low, high, tip, out _targetMin, out _targetMax);
            Minimum = _startMin = _targetMin;
            Maximum = _startMax = _targetMax;
            _animationTime = _animationSeconds = _stableTime = 0;
        }
        private void AnimateTo(double min, double max, double duration)
        {
            _startMin = Minimum; _startMax = Maximum;
            _targetMin = min; _targetMax = max;
            _animationTime = 0; _animationSeconds = duration;
        }
        public void Step(double low, double high, double tip, double seconds)
        {
            if (seconds <= 0) return;
            double width = _targetMax - _targetMin, margin = width * _settings.SafetyMargin;
            bool below = (_targetMin > 0 && tip < _targetMin + margin) || low < _targetMin;
            bool above = tip > _targetMax - margin || high > _targetMax;
            Range(low, high, tip, out double candidateMin, out double candidateMax);
            if (below || above)
            {
                // 额外留出扩展余量，避免连续上涨或下跌不断碰到同一条触发线。
                double reserve = Math.Max(width * 0.5, Math.Abs(tip) * _settings.MinimumSpanRatio);
                double min = Math.Min(_targetMin, candidateMin);
                double max = Math.Max(_targetMax, candidateMax);
                if (below) min = Math.Max(0, Math.Min(min, tip - reserve));
                if (above) max = Math.Max(max, tip + reserve);
                AnimateTo(min, max, _settings.ExpansionSeconds);
                _stableTime = 0;
            }
            else
            {
                _stableTime += seconds;
                if (_stableTime >= _settings.StableSeconds && candidateMax - candidateMin < width * 0.7)
                {
                    AnimateTo(candidateMin, candidateMax, _settings.ShrinkSeconds);
                    _stableTime = 0;
                }
            }
            if (_animationSeconds > 0)
            {
                _animationTime = Math.Min(_animationSeconds, _animationTime + seconds);
                double t = _animationTime / _animationSeconds;
                double blend = t * t * (3 - 2 * t);
                Minimum = _startMin + (_targetMin - _startMin) * blend;
                Maximum = _startMax + (_targetMax - _startMax) * blend;
            }
            // 巨幅变动时优先保证线头可见；普通扩展仍按 0.2 秒动画进行。
            double clearance = Math.Max(MinimumSpan * 0.05,
                Math.Max((Maximum - Minimum) * 0.02, Math.Abs(tip) * 0.02));
            Minimum = Math.Max(0, Math.Min(Minimum, tip - clearance));
            Maximum = Math.Max(Maximum, tip + clearance);
        }
    }
}
