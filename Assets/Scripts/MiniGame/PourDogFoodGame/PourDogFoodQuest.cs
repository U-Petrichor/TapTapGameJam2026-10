using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFoodQuest : SinglePointQuest
    {
        [SerializeField] private PourDogFood _pourDogFood;
        protected override PanelTaskPoint Point => _pourDogFood;
        private void Awake() { if (_pourDogFood == null) _pourDogFood = GetComponent<PourDogFood>(); }
        protected override PanelMiniGameInstance CreateInstance() => new PourDogFoodInstance(_pourDogFood, _rewardCoins, _rewardTalentPoints);
    }
}