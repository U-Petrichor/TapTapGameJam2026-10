using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CleanQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<CleanPoint> _cleanPoints = new();

        public void ConfigurePoints(IEnumerable<CleanPoint> points)
        {
            _cleanPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_cleanPoints.Contains(point)) _cleanPoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _cleanPoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new CleanInstance((CleanPoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
