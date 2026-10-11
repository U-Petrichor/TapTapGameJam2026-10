using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFoodQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<PourDogFoodPoint> _pourDogFoodPoints = new();

        public void ConfigurePoints(IEnumerable<PourDogFoodPoint> points)
        {
            _pourDogFoodPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_pourDogFoodPoints.Contains(point)) _pourDogFoodPoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _pourDogFoodPoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new PourDogFoodInstance((PourDogFoodPoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
