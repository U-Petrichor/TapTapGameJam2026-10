using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class SortFileQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<SortFile> _sortFilePoints = new();
        [SerializeField] private SortFile _sortFile;

        private void Awake() { }

        public void ConfigurePoints(IEnumerable<SortFile> points)
        {
            _sortFilePoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_sortFilePoints.Contains(point)) _sortFilePoints.Add(point);
            _sortFile = _sortFilePoints.Count > 0 ? _sortFilePoints[0] : null;
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _sortFilePoints) yield return point;
            if (_sortFile != null) yield return _sortFile;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new SortFileInstance((SortFile)point, _rewardCoins, _rewardTalentPoints);
    }
}
