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

        public GamePhaseChangedEvent(GamePhase newPhase)
        {
            NewPhase = newPhase;
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
        // 这里的bool值代表当前这个事件要求ESCMenu是开启还是关闭
        // 如果传进来是true，说明是打开ESCMenu
        // 如果传进来是false，说明是关闭ESCMenu
        public bool IsOpen { get; }
        public ESCMenuEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }

    /// <summary>
    /// 用来传递变化后的金币数和变化值
    /// </summary>
    public readonly struct EventCoinsChange : IEvent
    {
        public int RemainingCoins { get; }
        // TODO 这个变化值后续可以做动画
        public int ChangeValue { get; }
        public EventCoinsChange(int remainingCoins,int changeValue)
        {
            RemainingCoins = remainingCoins;
            ChangeValue = changeValue;
        }
    }
    /// <summary>
    /// 用来传递变化后的营养值和变化值
    /// </summary>
    public readonly struct EventNutritionChange : IEvent
    {
        public int RemainingNutrition { get; }
        // TODO 这个变化值后续可以做动画
        public int ChangeValue { get; }
        public EventNutritionChange(int remainingNutrition, int changeValue)
        {
            RemainingNutrition = remainingNutrition;
            ChangeValue = changeValue;
        }
    }
    public readonly struct EventCockroachCountChange : IEvent
    {
        public int RemainingCockroach { get; }
        // TODO 这个变化值后续可以做动画
        public int ChangeValue { get; }
        public EventCockroachCountChange(int remainingCockroach, int changeValue)
        {
            RemainingCockroach = remainingCockroach;
            ChangeValue = changeValue;
        }
    }
    public readonly struct EventGameOver : IEvent
    {
        public bool IsWin { get; }
        public string Reason { get; }
        public EventGameOver(bool isWin,string reason)
        {
            IsWin = isWin;
            Reason = reason;
        }
    }
}
