using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFoodQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<PourDogFood> _pourDogFoodPoints = new();
        [SerializeField] private PourDogFood _pourDogFood;

        private void Awake() { }

        public void ConfigurePoints(IEnumerable<PourDogFood> points)
        {
            _pourDogFoodPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_pourDogFoodPoints.Contains(point)) _pourDogFoodPoints.Add(point);
            _pourDogFood = _pourDogFoodPoints.Count > 0 ? _pourDogFoodPoints[0] : null;
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _pourDogFoodPoints) yield return point;
            if (_pourDogFood != null) yield return _pourDogFood;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new PourDogFoodInstance((PourDogFood)point, _rewardCoins, _rewardTalentPoints);
    }
}
