using UnityEngine;

namespace TapTapGameJam.Player
{
    public class MoveState : NpcStateBase
    {
        public override bool CanStartAttack => true;
        public MoveState(PlayerBasic owner) : base(owner) { }

        public override void Enter()
        {
            AnimationManager.PlayMove();
            Owner.PlayerMovement.SetMoveInput(InputManager.MoveInput);
        }

        public override void Update()
        {
            if (Owner.IsCarry)
            {
                Owner.SyncCarryState();
                return;
            }

            Owner.PlayerMovement.SetMoveInput(InputManager.MoveInput);
            if (InputManager.HasMoveInput == false)
            {
                StateMachine.ChangeState(Owner.Idle);
                return;
            }
        }
        public override void Exit()
        {
            Owner.PlayerMovement.SetMoveInput(Vector2.zero);
        }
    }
}
