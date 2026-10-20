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
    /// 事件携带 MiniGameKind，面板据此查找对应内容。
    /// </summary>
    public readonly struct EventOpenMiniGamePanel:IEvent
    {
        public MiniGameKind GameKind { get; }
        /// <summary>
        /// 这个需要所有小游戏在发布此事件时一并传出用于处理游戏完成或失败时的处理方法
        /// 并且接受一个bool值
        /// </summary>
        public Action<bool> OnFinished { get; }
        public TapTapGameJam.Player.PlayerBasic Player { get; }
        public object Owner { get; }
        public Action OnStarted { get; }
        public EventOpenMiniGamePanel(MiniGameKind gameKind, Action<bool> action,
            TapTapGameJam.Player.PlayerBasic player = null, object owner = null, Action onStarted = null){
            GameKind = gameKind;
            OnFinished = action;
            Player = player;
            Owner = owner;
            OnStarted = onStarted;
        }
    }
    /// <summary>
    /// 当玩家关闭小游戏时，使用此事件
    /// 目前主要作用
    /// 1. 通知玩家切换回DayPlayer的ActionMap
    /// </summary>
    public readonly struct EventCloseMiniGamePanel : IEvent { }

    // 关闭请求带上发起点位，避免旧任务取消时关闭另一个任务的面板。
    public readonly struct EventRequestCloseMiniGamePanel : IEvent
    {
        public object Owner { get; }
        public EventRequestCloseMiniGamePanel(object owner = null) => Owner = owner;
    }

    /// <summary>House 的出口请求结束购物，由 GamePhaseManager 更新真正的阶段。</summary>
    public readonly struct ShoppingFinishedEvent : IEvent { }

    /// <summary>夜晚渐亮完成，允许开始夜晚倒计时。</summary>
    public readonly struct NightTransitionCompletedEvent : IEvent { }

    public readonly struct GamePhaseChangedEvent : IEvent
    {
        public GamePhase NewPhase { get; }

        public GamePhaseChangedEvent(GamePhase newPhase)
        {
            NewPhase = newPhase;
        }
    }

    /// <summary>
    /// GamePhaseManager 每次进入白天后发布，第一天为 1。
    /// </summary>
    public readonly struct EventNewDay : IEvent
    {
        public int DayCount { get; }
        public EventNewDay(int dayCount)
        {
            DayCount = dayCount;
        }
    }
    /// <summary>成功完成一项复杂工作任务。</summary>
    public readonly struct EventComplexTaskCompleted : IEvent { }
    /// <summary>门首次达到开放日期；玩家靠近后自动开门。</summary>
    public readonly struct EventSecurityDoorAvailable : IEvent
    {
        public SecurityDoor Door { get; }
        public EventSecurityDoorAvailable(SecurityDoor door) => Door = door;
    }

    public readonly struct BackpackEvent : IEvent
    {
        public bool IsOpen { get; }
        public BackpackEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }

    /// <summary>
    /// 按下了背包键（B）。
    /// 只表示"这个键被按了"：到底开还是关由容器自己判断（持有"当前开/关"的是容器，不是输入层），
    /// ActionMap 也由 UIManager 按"有没有受监控容器开着"统一切。
    /// </summary>
    public readonly struct EventToggleBackpack : IEvent
    {
    }

    /// <summary>
    /// 受监控容器的开关状态变了（UIManager 判断完之后广播）。
    /// 只当"该重新判断暂停 / 别的横切逻辑"的触发器用；需要具体状态就去读
    /// <see cref="TapTapGameJam.UI.UIManager.AllContainersClosed"/>（这里也带了一份，方便日志）。
    /// </summary>
    public readonly struct EventContainerStateChanged : IEvent
    {
        /// <summary>是不是所有受监控的容器都关着</summary>
        public bool AllContainersClosed { get; }

        public EventContainerStateChanged(bool allContainersClosed)
        {
            AllContainersClosed = allContainersClosed;
        }
    }
    
    public readonly struct RepoOnOffEvent : IEvent
    {
        public bool IsOpen { get; }
        public RepoOnOffEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }
    
    public readonly struct StoreOnOffEvent : IEvent
    {
        public bool IsOpen { get; }
        public StoreOnOffEvent(bool isOpen)
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

    /// <summary>
    /// 玩家倒下（可用体力上限被受伤 / 中毒 / 负重吃空），由
    /// <c>PlayerHealth.HandleDefeat</c> 发布，**同一局只发一次**（那边的 <c>_isDefeated</c> 兜着）。
    ///
    /// 目前<b>还没有任何订阅方</b> —— 失败流程（倒下动画 / 结算界面 / 结束本局）接在这里：
    /// <code>
    /// EventPipeline.Subscribe&lt;EventPlayerDied&gt;(evt =&gt; { ... });
    /// </code>
    /// 注意它和 <see cref="EventGameOver"/> 不是一回事：后者是"分出胜负、结束本局"，
    /// 前者只是"玩家这条命没了"，两者之间的流程（谁先、要不要播动画）还没定。
    /// </summary>
    public readonly struct EventPlayerDied : IEvent
    {
    }

    /// <summary>
    /// 玩家身上的 Buff 集合重建了（天赋升降级、装备变化、临时效果到期）。
    /// 只带一个 Revision，不细分「哪个 key 变了」——读具体数值请直接问 PlayerBuffs。
    ///
    /// 需要刷新的东西：体力条上限、背包格数、守卫光锥、Buff 列表 UI。
    /// 注意：每帧都要读的数值（移速、蟑螂速度）不需要订阅它，直接读
    /// <c>PlayerBuffs.CockroachSnapshot</c> / <c>GetFactor</c> 就行。
    /// </summary>
    public readonly struct BuffsRebuiltEvent : IEvent
    {
        /// <summary>重建后的 Revision，和 <c>PlayerBuffs.Revision</c> 对得上</summary>
        public int Revision { get; }

        public BuffsRebuiltEvent(int revision)
        {
            Revision = revision;
        }
    }

    /// <summary>
    /// 教程系统的统一信号。业务代码只发“发生了什么”，是否弹窗由 TutorialDirector 判断。
    /// </summary>
    public readonly struct EventTutorialSignal : IEvent
    {
        public string Key { get; }

        public EventTutorialSignal(string key)
        {
            Key = key;
        }
    }
}
