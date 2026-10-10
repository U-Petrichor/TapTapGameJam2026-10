using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 开局一个开盘点，每半秒显示新报价，所有历史点通过横向压缩保留。
    public sealed class StockPointGame : MiniGameContent
    {
        [SerializeField] private StockPointChart _chart;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private Button _buyOne, _sellAll;
        [SerializeField] private StockPriceSettings _priceSettings = new();
        private StockRound _round;
        private Action _onCompleted;
        private bool _running;

        public bool IsConfigured => _chart != null && _buyOne != null && _sellAll != null;

        public void Configure(StockPointChart chart, TMP_Text status, TMP_Text result, Button buy, Button sell)
        {
            UnbindButtons();
            _chart = chart;
            _statusText = status;
            _resultText = result;
            _buyOne = buy;
            _sellAll = sell;
            BindButtons();
        }

        private void Awake() => BindButtons();

        private void BindButtons()
        {
            UnbindButtons();
            if (_buyOne != null) _buyOne.onClick.AddListener(Buy);
            if (_sellAll != null) _sellAll.onClick.AddListener(Sell);
        }

        private void UnbindButtons()
        {
            if (_buyOne != null) _buyOne.onClick.RemoveListener(Buy);
            if (_sellAll != null) _sellAll.onClick.RemoveListener(Sell);
        }

        public override void Begin(Action onCompleted)
        {
            ResetView();
            if (Resources == null) throw new InvalidOperationException("炒股需要本次交互玩家的资源组件。");
            var wallet = Resources;
            if (_chart != null && _statusText != null) _chart.SetPriceFont(_statusText.font);
            var generator = new StockPriceGenerator(wallet.Coins, () => UnityEngine.Random.value, _priceSettings);
            _round = new StockRound(() => wallet.Coins, wallet.TrySpendCoins, wallet.AddCoins, generator);
            _onCompleted = onCompleted;
            _running = true;
            Refresh();
        }

        public void Buy() { if (_running) { _round.Buy(); Refresh(); } }
        public void Sell() { if (_running) { _round.Sell(); Refresh(); } }

        private void Update()
        {
            if (!_running) return;
            _round.Tick(Time.unscaledDeltaTime);
            if (!_running || _round == null) return;
            Refresh();
            if (!_round.IsComplete) return;
            var completed = _onCompleted;
            End();
            completed?.Invoke();
        }

        private void Refresh()
        {
            if (_chart != null) _chart.SetPrices(_round.Prices, _round.Elapsed, _round.CurvePrices, _round.IsFinished);
            if (_statusText != null) _statusText.text = _round.IsFinished
                ? $"已结算     股价 {_round.Price:0.00}     今日金币 {Resources.Coins}     持仓 0"
                : $"剩余 {_round.RemainingSeconds:0.0} 秒     最新点 {_round.ConfirmedPrice:0.00}     资金 {_round.Balance:0.00}     持仓 {_round.Shares}";
            if (_resultText != null)
            {
                _resultText.color = _round.IsFinished
                    ? (_round.SettledProfit > 0 ? new Color(0.08f, 0.48f, 0.30f)
                        : _round.SettledProfit < 0 ? new Color(0.72f, 0.18f, 0.18f) : new Color(0.10f, 0.17f, 0.24f))
                    : new Color(0.10f, 0.17f, 0.24f);
                _resultText.text = _round.IsFinished
                    ? $"今日{(_round.SettledProfit > 0 ? "赚了" : _round.SettledProfit < 0 ? "亏了" : "持平")} · 结算 {_round.SettledProfit:+0;-0;0} 金币\n本局盈亏 {_round.Profit:+0.00;-0.00;0.00} · {_round.RemainingSeconds:0.0} 秒后结束"
                    : $"本局盈亏 {_round.Profit:+0.00;-0.00;0.00} · {(_round.LastTradePrice.HasValue ? $"最近成交 {_round.LastTradePrice.Value:0.00}" : "每 0.5 秒吐出一个报价点")}\n买卖与提前退出按待吐目标报价成交 · 最多持有 1 股";
            }
            if (_buyOne != null) _buyOne.interactable = _running && _round.CanBuy;
            if (_sellAll != null) _sellAll.interactable = _running && _round.CanSell;
        }

        public override void End()
        {
            _running = false;
            _onCompleted = null;
            _round?.Close();
            if (_buyOne != null) _buyOne.interactable = false;
            if (_sellAll != null) _sellAll.interactable = false;
        }

        public override void ResetView()
        {
            End();
            _round = null;
            if (_chart != null) _chart.SetPrices(null);
            if (_statusText != null) _statusText.text = "每局 20 秒 · 每 0.5 秒吐出一个点";
            if (_resultText != null) _resultText.text = "白天开放 · 每天 1 次 · 开盘先显示 1 个点";
        }

        private void OnDisable() => End();
        private void OnDestroy() { End(); UnbindButtons(); }
    }
}
