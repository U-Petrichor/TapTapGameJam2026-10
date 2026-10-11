namespace TapTapGameJam.MiniGame
{
    public class StockPoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "模拟炒股";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind => TapTapGameJam.Types.MiniGameKind.StockTrade;
        protected override bool KeepAvailableAfterAttempt => true;
    }
}
