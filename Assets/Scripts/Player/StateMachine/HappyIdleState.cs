using System.Collections;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class HappyIdleState : NpcStateBase
    {
        public HappyIdleState(PlayerBasic owner) : base(owner) { }
        public override void Enter()
        {
            AnimationManager.IsHappyIdleFinished = false;
            AnimationManager.PlayHappyIdle();
        }

        public override void Update()
        {
            if (Owner.IsCarry)
            {
                Owner.SyncCarryState();
                return;
            }

            // 小游戏期间也会进入此状态；移动不能依赖待机动画的结束事件。
            if (InputManager.HasMoveInput)
            {
                StateMachine.ChangeState(Owner.Move);
                return;
            }

            if(AnimationManager.IsHappyIdleFinished)
            {
                StateMachine.ChangeState(Owner.Idle);
            }
        }

    }
}
