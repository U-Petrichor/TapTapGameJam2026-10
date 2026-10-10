using TapTapGameJam.MiniGame;

namespace TapTapGameJam.Interactables
{
    public class TaskMakeCoffee : PanelTaskPoint
    {
        public override string InteractionPrompt => "制作咖啡";
        protected override int DefaultGameIndex => 4;
    }
}