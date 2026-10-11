using System;
using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFoodPoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "倒狗粮";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind => TapTapGameJam.Types.MiniGameKind.PourDogFood;
        public event Action<PlayerBasic> EventPourDogFoodFinish
        {
            add => EventCompleted += value;
            remove => EventCompleted -= value;
        }
    }
}
