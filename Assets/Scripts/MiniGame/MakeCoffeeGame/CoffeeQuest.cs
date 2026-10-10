using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CoffeeQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<TapTapGameJam.Interactables.TaskMakeCoffee> _coffeePoints = new();
        [SerializeField] private TapTapGameJam.Interactables.TaskMakeCoffee _coffeePoint;

        private void Awake() { }

        public void ConfigurePoints(IEnumerable<TapTapGameJam.Interactables.TaskMakeCoffee> points)
        {
            _coffeePoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_coffeePoints.Contains(point)) _coffeePoints.Add(point);
            _coffeePoint = _coffeePoints.Count > 0 ? _coffeePoints[0] : null;
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _coffeePoints) yield return point;
            if (_coffeePoint != null) yield return _coffeePoint;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new CoffeeInstance((TapTapGameJam.Interactables.TaskMakeCoffee)point, _rewardCoins, _rewardTalentPoints);
    }
}
