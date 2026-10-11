using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class PrinterRepairQuest : PanelMiniGameQuest
    {
        [SerializeField] private List<PrinterRepairPoint> _printerPoints = new();

        public void ConfigurePoints(IEnumerable<PrinterRepairPoint> points)
        {
            _printerPoints.Clear();
            if (points != null)
                foreach (var point in points)
                    if (point != null && !_printerPoints.Contains(point)) _printerPoints.Add(point);
        }

        protected override IEnumerable<PanelTaskPoint> GetPoints()
        {
            foreach (var point in _printerPoints) yield return point;
        }

        protected override PanelMiniGameInstance CreateInstance(PanelTaskPoint point) =>
            new PrinterRepairInstance((PrinterRepairPoint)point, _rewardCoins, _rewardTalentPoints);
    }
}
