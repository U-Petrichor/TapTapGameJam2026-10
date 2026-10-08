using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PrinterRepairQuest : SinglePointQuest
    {
        [SerializeField] private PrinterRepairPoint _printerPoint;
        protected override PanelTaskPoint Point => _printerPoint;
        private void Awake() { if (_printerPoint == null) _printerPoint = GetComponent<PrinterRepairPoint>(); }
        protected override PanelMiniGameInstance CreateInstance() =>
            new PrinterRepairInstance(_printerPoint, _rewardCoins, _rewardTalentPoints);
    }
}
