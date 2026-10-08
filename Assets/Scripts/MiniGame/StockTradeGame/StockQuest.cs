using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    // 固定白天入口。管理器在白天创建一次活动实例，用于保持交互和位置指引。
    public class StockQuest : MiniGameCandidate
    {
        [SerializeField] private StockTradePoint _stockPoint;
        private StockInstance _activity;
        public const int DailyPlayLimit = 1;
        private int _playsUsed;
        public int RemainingPlays => Mathf.Max(0, DailyPlayLimit - _playsUsed);
        public StockInstance Activity => _activity;
        public override bool IsDailyActivity => true;
        public override bool CanSpawn => isActiveAndEnabled && RemainingPlays > 0 && _activity == null && _stockPoint != null && _stockPoint.CanStartTask;
        public override void BeginNewDay() => _playsUsed = 0;
        private void Awake() { if (_stockPoint == null) _stockPoint = GetComponent<StockTradePoint>(); }
        public override MiniGameInstanceBasic CreateTask()
        {
            if (!CanSpawn) return null;
            var activity = new StockInstance(_stockPoint, () => RemainingPlays);
            _activity = activity;
            _stockPoint.EventAttemptStarted += HandleAttemptStarted;
            _stockPoint.EventAttemptFinished += HandleAttemptFinished;
            activity.EventFinished += HandleEnded;
            if (!activity.Begin()) return null;
            return activity;
        }
        private void HandleEnded(MiniGameInstanceBasic activity)
        {
            activity.EventFinished -= HandleEnded;
            _stockPoint.EventAttemptStarted -= HandleAttemptStarted;
            _stockPoint.EventAttemptFinished -= HandleAttemptFinished;
            if (_activity == activity) _activity = null;
        }
        private void HandleAttemptStarted()
        {
            _playsUsed++;
            _activity?.RefreshAvailability();
        }
        private void HandleAttemptFinished(bool success)
        {
            if (RemainingPlays == 0) _activity?.Cancel();
        }
        private void OnDisable() => _activity?.Cancel();
    }
}
