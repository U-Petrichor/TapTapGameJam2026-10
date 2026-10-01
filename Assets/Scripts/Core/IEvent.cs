using UnityEngine;
using TapTapGameJam.Types;

namespace TapTapGameJam.Events
{
    /// <summary>
    /// 所有游戏事件的标记接口。
    /// 它不声明任何成员，作用是：
    /// 1. 给 EventPipeline 做类型门禁（不是 IEvent 的东西发不进总线）
    /// 2. 让"全项目有哪些事件"可以用 IDE 的"查找实现"一次列出来
    /// 事件只负责描述"发生了什么"，打印/记录这类横切的事在 EventPipeline 里统一做
    /// </summary>
    public interface IEvent
    {
    }

    /// <summary>
    /// 定义此事件的原因是，在打开背包时要处理的事项有点多，音效、游戏暂停、切换UI等，接收方多所以用事件总线来广播
    /// </summary>
    public class InventoryControlEvent : IEvent
    {
        public InventoryControlEvent() { }
    }

    public readonly struct GamePhaseChangedEvent : IEvent
    {
        public GamePhase NewPhase { get; }
        public int DayIndex { get; }

        public GamePhaseChangedEvent(GamePhase newPhase, int dayIndex)
        {
            NewPhase = newPhase;
            DayIndex = dayIndex;
        }
    }
}
