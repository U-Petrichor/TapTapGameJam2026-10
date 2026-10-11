using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class MakeCoffeeQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<TapTapGameJam.Interactables.MakeCoffeePoint> _coffeePoints = new();

        public void ConfigurePoints(IEnumerable<TapTapGameJam.Interactables.MakeCoffeePoint> points)
        {
            _coffeePoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_coffeePoints.Contains(point)) _coffeePoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _coffeePoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new MakeCoffeeInstance((TapTapGameJam.Interactables.MakeCoffeePoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
