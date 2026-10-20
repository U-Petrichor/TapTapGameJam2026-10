using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 广播的种类。调用点直接写 <c>BroadcastUI.Broadcast(BroadcastKind.GuardKill)</c>，
    /// 不再手写字符串 —— 拼错会在编译期就报错，改名字也只需改这里。
    ///
    /// <see cref="InspectorNameAttribute"/> 让 Inspector 的映射表里显示中文，
    /// 设计配「哪个广播播哪段视频」时不用看英文枚举名。
    ///
    /// 新增一种广播：加一个枚举项 → 到 BroadcastPanel 预制体的映射表里补一条。
    /// </summary>
    public enum BroadcastKind
    {
        /// <summary>玩家被保安（猎杀者）打死</summary>
        [InspectorName("保安击杀")]
        GuardKill = 0,

        /// <summary>玩家被保洁的毒（喷雾）毒死</summary>
        [InspectorName("保洁毒杀致死")]
        CleanerPoison = 1,

        /// <summary>巢穴指令切换到「觅食」（点巢穴按钮）</summary>
        [InspectorName("出巢")]
        LeaveNest = 2,

        /// <summary>巢穴指令切换到「孵化」（点巢穴按钮）</summary>
        [InspectorName("归巢")]
        ReturnNest = 3,

        /// <summary>消杀开始</summary>
        [InspectorName("消杀时刻")]
        Disinfection = 4,
    }
}
