using UnityEngine;

namespace TapTapGameJam.Player
{
    public class CarryIdleState : PlayerStateBase
    {
        public override bool CanStartAttack => true;
        public CarryIdleState(PlayerBasic owner) : base(owner) { }

        public override void Enter()
        {
            AnimationManager.PlayCarryIdle(Owner.CarryType);
            Owner.PlayerMovement.SetMoveInput(Vector2.zero);
        }

        public override void Update()
        {
            if (!Owner.IsCarry)
            {
                Owner.SyncCarryState();
                return;
            }

            Owner.PlayerMovement.SetMoveInput(Vector2.zero);
            if (InputManager.HasMoveInput)
            {
                StateMachine.ChangeState(Owner.CarryAndMove);
            }
        }
        public override void Exit()
        {
            Owner.PlayerMovement.SetMoveInput(Vector2.zero);
        }
    }
}
