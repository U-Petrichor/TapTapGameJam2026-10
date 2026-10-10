using System;
using System.Collections.Generic;

namespace TapTapGameJam.UI
{
    // 历史报价与两个前瞻报价分离；交易使用当前绘制目标，钱包仅结算净盈亏一次。
    public sealed class StockRound
    {
        public const int VisiblePoints = 12;
        public const int InitialPoints = 1;
        public const double Duration = 20;
        public const double ResultSeconds = 1.5;
        public const double TradingSeconds = Duration - ResultSeconds;
        public const double RefreshSeconds = 0.5;
        public const int RefreshedPoints = (int)(TradingSeconds / RefreshSeconds);
        public const int MaxPoints = InitialPoints + RefreshedPoints;
        private readonly Func<int> _getBalance;
        private readonly Func<int, bool> _spend;
        private readonly Action<int> _credit;
        private readonly IStockPriceGenerator _generator;
        private readonly float _priceCeiling;
        private readonly List<float> _prices = new();
        private readonly List<float> _curvePrices = new();
        private bool _hasBought, _closed;
        private int _refreshCount;
        private float _settlementPrice;
        public IReadOnlyList<float> Prices => _prices;
        public IReadOnlyList<float> CurvePrices => _curvePrices;
        public float ConfirmedPrice => _prices[_prices.Count - 1];
        public float Price => IsFinished ? _settlementPrice
            : _refreshCount >= RefreshedPoints ? ConfirmedPrice : _curvePrices[_prices.Count];
        public float? LastTradePrice { get; private set; }
        public int Shares { get; private set; }
        public int InitialBalance { get; }
        public double Balance { get; private set; }
        public double Profit => Balance + Shares * (double)Price - InitialBalance;
        public double Elapsed { get; private set; }
        public double RemainingSeconds => Math.Max(0, Duration - Elapsed);
        public bool IsFinished { get; private set; }
        public bool IsComplete => _closed || Elapsed >= Duration;
        public int SettledProfit { get; private set; }
        public bool CanBuy => !IsFinished && InitialBalance > 0 && Shares == 0 && Balance >= Price;
        public bool CanSell => !IsFinished && Shares == 1;

        public StockRound(Func<int> getBalance, Func<int, bool> spend, Action<int> credit,
            IStockPriceGenerator generator)
        {
            _getBalance = getBalance ?? throw new ArgumentNullException(nameof(getBalance));
            _spend = spend ?? throw new ArgumentNullException(nameof(spend));
            _credit = credit ?? throw new ArgumentNullException(nameof(credit));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            InitialBalance = Math.Max(0, getBalance());
            _priceCeiling = InitialBalance;
            if ((double)_priceCeiling > InitialBalance)
                _priceCeiling = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(_priceCeiling) - 1);
            Balance = InitialBalance;
            _curvePrices.Add(ValidatePrice(generator.InitialPrice));
            _prices.AddRange(_curvePrices);
            RefillLookahead();
        }
        private float ValidatePrice(float price)
        {
            if (float.IsNaN(price) || float.IsInfinity(price) || price < 0)
                throw new InvalidOperationException("Stock generator returned an invalid price.");
            return !_hasBought ? Math.Min(price, _priceCeiling) : price;
        }
        private void GeneratePrice() => _curvePrices.Add(ValidatePrice(_generator.NextPrice(
            _curvePrices[_curvePrices.Count - 1], _hasBought ? (float?)null : _priceCeiling)));
        private void RefillLookahead()
        {
            // 已生成报价保持不变；交易和下一次确认使用同一个目标价。
            while (_curvePrices.Count < _prices.Count + 2) GeneratePrice();
        }
        public bool Buy()
        {
            if (!CanBuy) return false;
            float price = Price;
            Balance -= price;
            LastTradePrice = price;
            Shares = 1;
            _hasBought = true;
            return true;
        }
        public bool Sell()
        {
            if (!CanSell) return false;
            float price = Price;
            Balance += price;
            LastTradePrice = price;
            Shares = 0;
            return true;
        }
        public void Tick(double seconds)
        {
            if (IsComplete || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            Elapsed = Math.Min(Duration, Elapsed + seconds);
            while (!IsFinished && _refreshCount < RefreshedPoints
                && (_refreshCount + 1) * RefreshSeconds <= Elapsed)
            {
                _prices.Add(_curvePrices[_prices.Count]);
                _refreshCount++;
                if (_refreshCount < RefreshedPoints) RefillLookahead();
            }
            if (Elapsed >= TradingSeconds) Settle();
        }
        private void Settle()
        {
            if (IsFinished) return;
            // 正常结束使用最后完成的报价；提前退出仍使用当前绘制目标。
            _settlementPrice = Price;
            Sell();
            double rounded = Math.Round(Profit, MidpointRounding.AwayFromZero);
            int walletBalance = Math.Max(0, _getBalance());
            SettledProfit = (int)Math.Max(-walletBalance, Math.Min((double)int.MaxValue - walletBalance, rounded));
            IsFinished = true;
            if (SettledProfit < 0 && !_spend(-SettledProfit))
                throw new InvalidOperationException("Wallet cannot settle stock loss.");
            if (SettledProfit > 0) _credit(SettledProfit);
        }
        public void Close() { Settle(); _closed = true; }
    }
}
