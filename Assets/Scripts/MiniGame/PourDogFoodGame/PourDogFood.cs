using System;
using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public class PourDogFood : PanelTaskPoint
    {
        public override string InteractionPrompt => "倒狗粮";
        protected override int DefaultGameIndex => 1;
        public event Action<PlayerBasic> EventPourDogFoodFinish
        {
            add => EventCompleted += value;
            remove => EventCompleted -= value;
        }
    }
}