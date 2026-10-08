using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class CleanQuest : SinglePointQuest
    {
        [SerializeField] private CleanMopWater _cleanPoint;
        protected override PanelTaskPoint Point => _cleanPoint;
        private void Awake() { if (_cleanPoint == null) _cleanPoint = GetComponent<CleanMopWater>(); }
        protected override PanelMiniGameInstance CreateInstance() => new CleanInstance(_cleanPoint, _rewardCoins, _rewardTalentPoints);
    }
}
