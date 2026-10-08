using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CoffeeQuest : SinglePointQuest
    {
        [SerializeField] private TapTapGameJam.Interactable.TaskMakeCoffee _coffeePoint;
        protected override PanelTaskPoint Point => _coffeePoint;
        private void Awake() { if (_coffeePoint == null) _coffeePoint = GetComponent<TapTapGameJam.Interactable.TaskMakeCoffee>(); }
        protected override PanelMiniGameInstance CreateInstance() => new CoffeeInstance(_coffeePoint, _rewardCoins, _rewardTalentPoints);
    }
}
