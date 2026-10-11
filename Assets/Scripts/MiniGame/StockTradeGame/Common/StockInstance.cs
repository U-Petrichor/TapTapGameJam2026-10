using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    // 当天次数耗尽或离开白天时关闭入口；每局资金由 StockPointGame 结算。
    public sealed class StockInstance : MiniGameInstanceBasic
    {
        private readonly StockPoint _point;
        private bool _begun;
        private readonly System.Func<int> _remainingPlays;
        public override bool IsDailyActivity => true;
        public override string AvailabilityText => $"白天开放 · 今日剩余 {_remainingPlays?.Invoke() ?? StockQuest.DailyPlayLimit} 次";
        public StockPoint Point => _point;
        public StockInstance(StockPoint point, System.Func<int> remainingPlays = null) : base(point.transform)
        {
            _point = point;
            _remainingPlays = remainingPlays;
            GuideText = "模拟炒股";
        }
        public bool Begin()
        {
            if (_begun || IsFinished || _point == null) return false;
            _begun = true;
            _point.EventCompleted += HandleRoundCompleted;
            _point.EventTaskCancelled += Cancel;
            if (_point.StartTask(this)) return true;
            _begun = false;
            Cancel();
            return false;
        }
        private void HandleRoundCompleted(PlayerBasic player) => NotifyStageChanged();
        public void RefreshAvailability() => NotifyStageChanged();
        protected override void Release()
        {
            if (_point == null) return;
            _point.EventCompleted -= HandleRoundCompleted;
            _point.EventTaskCancelled -= Cancel;
            if (_begun) _point.CancelTask(this);
        }
    }
}
