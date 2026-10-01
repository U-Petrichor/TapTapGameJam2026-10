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
    public readonly struct BackpackEvent : IEvent
    {
        public bool IsOpen { get; }
        public BackpackEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }
    public readonly struct ESCMenuEvent : IEvent
    {
        // 负责通知打开游戏内ESC的菜单，此时应该停止游戏进行，直到菜单关闭
        // 这里传达Bool值是为了区分打开还是关闭，这个必须在玩家侧确定，因为玩家输入侧还需要修改ActionMap的状态
        public bool IsOpen { get; }
        public ESCMenuEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }

}
