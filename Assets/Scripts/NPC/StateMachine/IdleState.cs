using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>NPC 待机状态：停止移动并等待一段时间，然后进入 Walk。</summary>
    public sealed class IdleState : NpcStateBase
    {
        private float _remainingTime;
        public IdleState(NPC owner) : base(owner) { }

        public override void Enter()
        {
            _remainingTime = Owner.GetNextIdleDuration();
            Owner.StopMoving();
            Owner.PlayIdleAnimation();
        }

        public override void Update()
        {
            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
            {
                StateMachine.ChangeState(Owner.Walk);
            }
        }
    }
}
