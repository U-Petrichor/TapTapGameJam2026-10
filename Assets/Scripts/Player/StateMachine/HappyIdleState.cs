using System.Collections;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class HappyIdleState : PlayerStateBase
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

            if(AnimationManager.IsHappyIdleFinished)
            {
                StateMachine.ChangeState(Owner.Idle);
            }
        }

    }
}
