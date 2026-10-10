using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PrinterRepairQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<PrinterRepairPoint> _printerPoints = new();
        [SerializeField] private PrinterRepairPoint _printerPoint;

        private void Awake() { }

        public void ConfigurePoints(IEnumerable<PrinterRepairPoint> points)
        {
            _printerPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_printerPoints.Contains(point)) _printerPoints.Add(point);
            _printerPoint = _printerPoints.Count > 0 ? _printerPoints[0] : null;
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _printerPoints) yield return point;
            if (_printerPoint != null) yield return _printerPoint;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new PrinterRepairInstance((PrinterRepairPoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
