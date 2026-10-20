using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 结局的跨场景传递：GameScene 里记下"这局是怎么结束的"，
    /// MainMenu 加载后读一次就清空。
    ///
    /// 和 <see cref="SaveManager.ConsumePendingLoad"/> 一个套路：**只在内存里，不写盘**。
    /// 结局不该跨进程残留 —— 重开游戏应该直接看到主菜单，而不是又弹一次结局图。
    /// </summary>
    public static class EndingState
    {
        /// <summary>待播放的结局。没有就是 null（正常从主菜单进来 / 用 ESC 回的主菜单）。</summary>
        public static GameEnding? Pending { get; private set; }

        public static bool HasPending => Pending.HasValue;

        /// <summary>记下这一局的结局。</summary>
        public static void Set(GameEnding ending)
        {
            Pending = ending;
        }

        /// <summary>取出并清空。MainMenu 只调一次。</summary>
        public static GameEnding? Consume()
        {
            GameEnding? ending = Pending;
            Pending = null;
            return ending;
        }

        /// <summary>
        /// 关掉"进入 Play 模式不重载 Domain"时静态变量残留。
        /// 项目里 SaveManager / PlayerProfile 用的都是这一套。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Pending = null;
        }
    }
}
