using TapTapGameJam.MiniGame;

namespace TapTapGameJam.MiniGame
{
    public sealed class ParcelCutPoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "拆快递";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind =>
            TapTapGameJam.Types.MiniGameKind.ParcelCut;
    }
}
