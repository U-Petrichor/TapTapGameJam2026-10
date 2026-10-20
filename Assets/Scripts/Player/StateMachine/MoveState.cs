using TapTapGameJam.Tutorial;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class MoveState : NpcStateBase
    {
        private static bool firstMoveSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFirstMove() => firstMoveSent = false;

        /// <summary>QA 重置教程进度时一并清掉“本次运行已经发过首次移动”的标记。</summary>
        public static void ResetFirstMoveTracking() => firstMoveSent = false;

        public override bool CanStartAttack => true;
        public MoveState(PlayerBasic owner) : base(owner) { }

        public override void Enter()
        {
            AnimationManager.PlayMove();
            Owner.PlayerMovement.SetMoveInput(InputManager.MoveInput);

            if (!firstMoveSent)
            {
                firstMoveSent = true;
                TutorialSignal.Send(TutorialSignals.MoveFirst);
            }
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
