using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    // 固定白天入口。管理器在白天创建一次活动实例，用于保持交互和位置指引。
    public class StockQuest : MiniGameCandidate
    {
        [SerializeField] private List<StockTradePoint> _stockPoints = new();
        [SerializeField] private StockTradePoint _stockPoint;
        private StockInstance _activity;
        public const int DailyPlayLimit = 999;
        private int _playsUsed;
        public int RemainingPlays => Mathf.Max(0, DailyPlayLimit - _playsUsed);
        public StockInstance Activity => _activity;
        public override bool IsDailyActivity => true;
        public override bool CanSpawn => isActiveAndEnabled && RemainingPlays > 0 && _activity == null && FindAvailablePoint() != null;
        public override void BeginNewDay() => _playsUsed = 0;

        public void ConfigurePoints(IEnumerable<StockTradePoint> points)
        {
            _stockPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_stockPoints.Contains(point)) _stockPoints.Add(point);
            _stockPoint = _stockPoints.Count > 0 ? _stockPoints[0] : null;
        }
        public override MiniGameInstanceBasic CreateTask()
        {
            StockTradePoint point = FindAvailablePoint();
            if (!isActiveAndEnabled || RemainingPlays <= 0 || _activity != null || point == null)
                return null;

            var activity = new StockInstance(point, () => RemainingPlays);
            _activity = activity;
            point.EventAttemptStarted += HandleAttemptStarted;
            point.EventAttemptFinished += HandleAttemptFinished;
            activity.EventFinished += HandleEnded;
            if (!activity.Begin()) return null;
            return activity;
        }
        private void HandleEnded(MiniGameInstanceBasic activity)
        {
            activity.EventFinished -= HandleEnded;
            if (activity is StockInstance stock && stock.Point != null)
            {
                stock.Point.EventAttemptStarted -= HandleAttemptStarted;
                stock.Point.EventAttemptFinished -= HandleAttemptFinished;
            }
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

        private StockTradePoint FindAvailablePoint()
        {
            var seen = new HashSet<StockTradePoint>();
            foreach (var point in _stockPoints)
                if (point != null && seen.Add(point) && point.CanStartTask)
                    return point;
            if (_stockPoint != null && seen.Add(_stockPoint) && _stockPoint.CanStartTask)
                return _stockPoint;
            return null;
        }
    }
}
