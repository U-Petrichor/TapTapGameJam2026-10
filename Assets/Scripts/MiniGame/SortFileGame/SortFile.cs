using System;
using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public class SortFile : PanelTaskPoint
    {
        public override string InteractionPrompt => "分拣文件";
        protected override int DefaultGameIndex => 2;
        public event Action<PlayerBasic> EventSortFileFinish
        {
            add => EventCompleted += value;
            remove => EventCompleted -= value;
        }
    }
}