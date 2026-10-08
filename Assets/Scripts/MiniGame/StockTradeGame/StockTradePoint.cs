namespace TapTapGameJam.MiniGame
{
    public class StockTradePoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "模拟炒股";
        protected override int DefaultGameIndex => 3;
        protected override bool KeepAvailableAfterAttempt => true;
    }
}
