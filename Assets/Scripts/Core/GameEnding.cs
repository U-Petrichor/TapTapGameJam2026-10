namespace TapTapGameJam.Core
{
    /// <summary>
    /// 本局是怎么结束的。决定 MainMenu 里放哪张结局图。
    /// 对应 Assets/UI/结局/ 下的五张图。
    /// </summary>
    public enum GameEnding
    {
        /// <summary>踩死：玩家被保安（猎杀者）打死</summary>
        Crushed = 0,

        /// <summary>喷死：玩家被保洁的喷雾毒死</summary>
        Sprayed = 1,

        /// <summary>过劳死：撑到第 10 天还没赢</summary>
        Overwork = 2,

        /// <summary>区域消杀：消杀把场上蟑螂清成 0</summary>
        Disinfection = 3,

        /// <summary>占领：天亮时蟑螂数量达标，或者玩家已经拿到「群颚破壁」</summary>
        Occupation = 4,
    }
}
