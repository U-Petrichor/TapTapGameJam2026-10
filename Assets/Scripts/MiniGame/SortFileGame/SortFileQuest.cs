using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class SortFileQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<SortFilePoint> _sortFilePoints = new();

        public void ConfigurePoints(IEnumerable<SortFilePoint> points)
        {
            _sortFilePoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_sortFilePoints.Contains(point)) _sortFilePoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _sortFilePoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new SortFileInstance((SortFilePoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
