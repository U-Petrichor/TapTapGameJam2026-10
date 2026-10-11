using TapTapGameJam.MiniGame;

namespace TapTapGameJam.Interactables
{
    public class MakeCoffeePoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "制作咖啡";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind => TapTapGameJam.Types.MiniGameKind.MakeCoffee;
    }
}
