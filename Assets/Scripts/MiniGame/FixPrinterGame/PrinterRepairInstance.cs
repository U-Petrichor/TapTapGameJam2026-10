namespace TapTapGameJam.MiniGame
{
    public sealed class PrinterRepairInstance : PanelMiniGameInstance
    {
        public PrinterRepairInstance(PrinterRepairPoint point, int coins, int talent)
            : base(point, "修复打印机", coins, talent) { }
    }
}
