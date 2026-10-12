namespace TapTapGameJam.NPC
{
    /// <summary>NPC 状态基类。状态实例在构造时绑定所属 NPC，并在运行时复用。</summary>
    public abstract class NpcStateBase
    {
        protected readonly NPC Owner;
        protected NpcStateMachine StateMachine => Owner.StateMachine;

        protected NpcStateBase(NPC owner)
        {
            Owner = owner;
        }

        public abstract void Enter();
        public abstract void Update();
        public virtual void Exit() { }
    }
}
