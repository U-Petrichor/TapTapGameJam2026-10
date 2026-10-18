using System;

namespace TapTapGameJam.UI
{
    public interface IStockPriceGenerator
    {
        float InitialPrice { get; }
        float NextPrice(float currentPrice, float? ceiling);
    }

    [Serializable]
    public sealed class StockPriceSettings
    {
        public float ShockProbability = 0.1f;
        public float TrendVariation = 0.65f;
        public float GentleMinRatio = 0.03f;
        public float GentleMaxRatio = 0.07f;
        public float PullbackProbability = 0.25f;
        public float ShockMinRatio = 0.3f;
        public float ShockMaxRatio = 0.7f;
    }

    // 与交易、Unity 和图表解耦；后续公式可通过 IStockPriceGenerator 替换。
    public sealed class StockPriceGenerator : IStockPriceGenerator
    {
        private readonly Func<double> _random;
        private readonly StockPriceSettings _settings;
        private readonly float _capital;
        private float _trend;
        private int _pullbackSteps;
        private int _forwardSteps;
        private bool _recovering;
        public float InitialPrice { get; }
        public StockPriceGenerator(int capital, Func<double> random, StockPriceSettings settings = null)
        {
            _capital = Math.Max(0, capital);
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _settings = settings ?? new StockPriceSettings();
            ValidateSettings();
            InitialPrice = _capital * Range(0.5f, 0.9f);
            ResetTrend(InitialPrice);
        }
        private void ValidateSettings()
        {
            if (!InRange(_settings.ShockProbability, 0, 1) || !InRange(_settings.TrendVariation, 0, 1)
                || !InRange(_settings.PullbackProbability, 0, 1)
                || !InRange(_settings.GentleMinRatio, 0.0001f, 1)
                || !InRange(_settings.GentleMaxRatio, _settings.GentleMinRatio, 1)
                || !InRange(_settings.ShockMinRatio, 0, 1)
                || !InRange(_settings.ShockMaxRatio, _settings.ShockMinRatio, 1))
                throw new ArgumentOutOfRangeException(nameof(_settings), "Invalid stock price settings.");
        }
        private static bool InRange(float value, float min, float max) =>
            !float.IsNaN(value) && value >= min && value <= max;
        private double RandomValue()
        {
            double value = _random();
            if (double.IsNaN(value) || value < 0 || value > 1)
                throw new InvalidOperationException("Stock random value must be between zero and one.");
            return value;
        }
        private float Range(float min, float max) => min + (max - min) * (float)RandomValue();
        private void ResetTrend(float price, bool downward = false)
        {
            float magnitude = Math.Max(price, _capital * 0.1f)
                * Range(_settings.GentleMinRatio, _settings.GentleMaxRatio);
            _trend = (downward || RandomValue() < 0.5) ? -magnitude : magnitude;
            _pullbackSteps = _forwardSteps = 0;
        }
        public float NextPrice(float currentPrice, float? ceiling)
        {
            bool shock = !_recovering && RandomValue() < _settings.ShockProbability;
            _recovering = false;
            float delta;
            if (shock)
            {
                float magnitude = currentPrice * Range(_settings.ShockMinRatio, _settings.ShockMaxRatio);
                delta = RandomValue() < 0.5 ? -magnitude : magnitude;
            }
            else
            {
                if (_trend == 0) ResetTrend(currentPrice);
                // 至少走两步主趋势，再允许连续一到两步回调；回调不改变主趋势。
                if (_pullbackSteps == 0 && _forwardSteps >= 2
                    && RandomValue() < _settings.PullbackProbability)
                {
                    _pullbackSteps = RandomValue() < 0.5 ? 1 : 2;
                    _forwardSteps = 0;
                }
                if (_pullbackSteps > 0)
                {
                    delta = -_trend * Range(0.5f, 1f);
                    _pullbackSteps--;
                }
                else
                {
                    delta = _trend * Range(1 - _settings.TrendVariation, 1 + _settings.TrendVariation);
                    _forwardSteps = Math.Min(2, _forwardSteps + 1);
                }
                if (delta < 0) delta = -Math.Min(-delta, currentPrice * 0.5f);
            }
            float next = (float)Math.Max(0, Math.Min(float.MaxValue, (double)currentPrice + delta));
            bool capped = ceiling.HasValue && next >= ceiling.Value;
            if (ceiling.HasValue) next = Math.Min(next, ceiling.Value);
            if (shock)
            {
                ResetTrend(next, capped);
                _recovering = true; // 突变后的下一跳必定恢复平缓，不沿用突变差值。
            }
            else if (capped) ResetTrend(next, true);
            // 保留独立的趋势步幅，避免随机系数连乘让走势逐渐变平。
            return next;
        }
    }
}
