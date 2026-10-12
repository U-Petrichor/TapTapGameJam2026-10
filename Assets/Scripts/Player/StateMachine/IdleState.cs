using UnityEngine;

namespace TapTapGameJam.Player
{
    public class IdleState : NpcStateBase
    {
        private float _idleTimer;
        private const float CHANGE_IDLE = 10f;
        public IdleState(PlayerBasic owner) : base(owner) { }

        public override void Enter()
        {
            _idleTimer = 0f;
            AnimationManager.PlayIdle();
        }

        public override void Update()
        {
            if (Owner.IsCarry)
            {
                Owner.SyncCarryState();
                return;
            }

            // 一直计时，当玩家长时间不操作，就播放额外的Idle动作
            _idleTimer += Time.deltaTime;
            if (InputManager.HasMoveInput == true)
            {
                // 说明此时有移动，切换为MoveState
                StateMachine.ChangeState(Owner.Move);
            }

            if(_idleTimer> CHANGE_IDLE)
            {
                // 这里应播放另一种Idle动画，同时将时间清0
                StateMachine.ChangeState(Owner.HappyIdle);
                _idleTimer = 0f;
            }

        }
    }
}
