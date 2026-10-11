namespace TapTapGameJam.MiniGame
{
    public class PrinterRepairPoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "敲打修复打印机";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind => TapTapGameJam.Types.MiniGameKind.PrinterRepair;
    }
}
