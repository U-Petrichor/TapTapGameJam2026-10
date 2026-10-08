using UnityEngine;
using TapTapGameJam.Types;
using System;

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
    /// 当玩家交互部分小游戏时，使用此事件
    /// 目前主要作用：
    /// 1. 通知MiniGamePanelUI打开
    /// 2. 通知玩家切换ActionMap
    /// index = 0 ->清理拖把污水的任务
    /// index = 1 ->
    /// index = 2 ->
    /// index = 3 ->
    /// index = 4 ->
    /// index = 5 ->
    /// index = 6 ->
    /// index = 7 ->
    /// index = 8 ->
    /// </summary>
    public readonly struct EventOpenMiniGamePanel:IEvent
    {
        // MiniGameIndex打算在控制的Canvas内声明并设置，这样一个小游戏自己有一个编号
        // 当要执行对应的小游戏，需要打开对应的Panel时只需要传一个整形就行；
        public int MiniGameIndex { get; }
        /// <summary>
        /// 这个需要所有小游戏在发布此事件时一并传出用于处理游戏完成或失败时的处理方法
        /// 并且接受一个bool值
        /// </summary>
        public Action<bool> OnFinished { get; }
        public EventOpenMiniGamePanel(int miniGameIndex,Action<bool> action){
            MiniGameIndex = miniGameIndex;
            OnFinished = action;
        }
    }
    /// <summary>
    /// 当玩家关闭小游戏时，使用此事件
    /// 目前主要作用
    /// 1. 通知玩家切换回DayPlayer的ActionMap
    /// </summary>
    public readonly struct EventCloseMiniGamePanel : IEvent { }

    public readonly struct GamePhaseChangedEvent : IEvent
    {
        public GamePhase NewPhase { get; }

        public GamePhaseChangedEvent(GamePhase newPhase)
        {
            NewPhase = newPhase;
        }
    }

    /// <summary>
    /// 这个事件在GameManager当中发布，每次到白天时会更新，区间为[1,+无穷]
    /// </summary>
    public readonly struct EventNewDay : IEvent
    {
        public int DayCount { get; }
        public EventNewDay(int dayCount)
        {
            DayCount = dayCount;
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
        public float RemainingNutrition { get; }
        // TODO 这个变化值后续可以做动画
        public float ChangeValue { get; }
        public EventNutritionChange(float remainingNutrition, float changeValue)
        {
            RemainingNutrition = remainingNutrition;
            ChangeValue = changeValue;
        }
    }
    public readonly struct EventTalentPointChange : IEvent
    {
        public int RemainingTalentPoint { get; }
        public int ChangeValue { get; }
        public EventTalentPointChange(int remainingTalentPoint,int changeValue){
            RemainingTalentPoint = remainingTalentPoint;
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
    public readonly struct EventCoffeeCountChange : IEvent
    {
        public int CoffeeCount { get; }
        public EventCoffeeCountChange(int coffeeCount) => CoffeeCount = coffeeCount;
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
