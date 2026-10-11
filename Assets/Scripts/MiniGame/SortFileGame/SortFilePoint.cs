using System;
using TapTapGameJam.Player;

namespace TapTapGameJam.MiniGame
{
    public class SortFilePoint : PanelTaskPoint
    {
        public override string InteractionPrompt => "分拣文件";
        protected override TapTapGameJam.Types.MiniGameKind DefaultGameKind => TapTapGameJam.Types.MiniGameKind.SortFile;
        public event Action<PlayerBasic> EventSortFileFinish
        {
            add => EventCompleted += value;
            remove => EventCompleted -= value;
        }
    }
}
