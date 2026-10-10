using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CleanQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<CleanMopWater> _cleanPoints = new();
        [SerializeField] private CleanMopWater _cleanPoint;

        private void Awake() { }

        public void ConfigurePoints(IEnumerable<CleanMopWater> points)
        {
            _cleanPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_cleanPoints.Contains(point)) _cleanPoints.Add(point);
            _cleanPoint = _cleanPoints.Count > 0 ? _cleanPoints[0] : null;
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _cleanPoints) yield return point;
            if (_cleanPoint != null) yield return _cleanPoint;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new CleanInstance((CleanMopWater)point, _rewardCoins, _rewardTalentPoints);
    }
}
