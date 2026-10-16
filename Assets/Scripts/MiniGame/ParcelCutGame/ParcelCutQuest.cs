using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public sealed class ParcelCutQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<ParcelCutPoint> _parcelCutPoints = new();

        public void ConfigurePoints(IEnumerable<ParcelCutPoint> points)
        {
            _parcelCutPoints.Clear();
            if (points == null) return;
            foreach (var point in points)
                if (point != null && !_parcelCutPoints.Contains(point)) _parcelCutPoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _parcelCutPoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new ParcelCutInstance((ParcelCutPoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
