using UnityEngine;

namespace TapTapGameJam.Player
{
    public class CarryAndMoveState : PlayerStateBase
    {
        public override bool CanStartAttack => true;
        public CarryAndMoveState(PlayerBasic owner) : base(owner) { }

        public override void Enter()
        {
            AnimationManager.PlayCarryAndMove(Owner.CarryType);
            Owner.PlayerMovement.SetMoveInput(InputManager.MoveInput);
        }

        public override void Update()
        {
            if (!Owner.IsCarry)
            {
                Owner.SyncCarryState();
                return;
            }

            Owner.PlayerMovement.SetMoveInput(InputManager.MoveInput);
            if (InputManager.HasMoveInput == false)
            {
                StateMachine.ChangeState(Owner.CarryIdle);
                return;
            }
        }
        public override void Exit()
        {
            Owner.PlayerMovement.SetMoveInput(Vector2.zero);
        }
    }
}
