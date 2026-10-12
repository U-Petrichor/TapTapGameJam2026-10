namespace TapTapGameJam.Player
{
    /// <summary>
    /// 怪物状态的基类：状态对象在构造时绑定所属的怪物，之后反复复用。
    /// 每只怪各持有一份自己的状态实例，状态里的计时器字段不会被其它怪共享。
    /// </summary>
    public abstract class NpcStateBase
    {
        protected readonly PlayerBasic Owner;
        // protected MonsterInstance Instance => Owner.Instance;
        protected PlayerStateMachine StateMachine => Owner.StateMachine;
        protected PlayerInputManager InputManager => Owner.InputManager;
        protected PlayerAnimationManager AnimationManager => Owner.AnimationManager;
        /// <summary>
        ///  所有状态都默认不能进行攻击，但是部分状态可以，例如Idle与Move
        ///  允许攻击的状态要重写此布尔值
        /// </summary>
        public virtual bool CanStartAttack => false;

        protected NpcStateBase(PlayerBasic owner)
        {
            Owner = owner;
        }

        public abstract void Enter();
        public abstract void Update();
        public virtual void Exit() { }
    }
}
