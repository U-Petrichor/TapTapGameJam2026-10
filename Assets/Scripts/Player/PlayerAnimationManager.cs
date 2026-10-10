using System.Collections;
using UnityEngine;

namespace TapTapGameJam.Player
{
    public class PlayerAnimationManager : MonoBehaviour
    {
        [field: SerializeField] public Animator Animator { get; private set; }

        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int HappyIdleStateHash = Animator.StringToHash("Base Layer.HappyIdle");
        private static readonly int MoveStateHash = Animator.StringToHash("Base Layer.Move");
        private static readonly int CarryIdleStateHash = Animator.StringToHash("Base Layer.CarryIdle");
        private static readonly int CarryAndMoveStateHash = Animator.StringToHash("Base Layer.CarryAndMove");
        private static readonly int FileCarryIdleStateHash = Animator.StringToHash("Base Layer.FileCarryIdle");
        private static readonly int FileCarryAndMoveStateHash = Animator.StringToHash("Base Layer.FileCarryAndMove");
        private static readonly int NestCarryIdleStateHash = Animator.StringToHash("Base Layer.NestCarryIdle");
        private static readonly int NestCarryAndMoveStateHash = Animator.StringToHash("Base Layer.NestCarryAndMove");

        public bool IsHappyIdleFinished = false;


        public void PlayIdle()
        {
            Animator.CrossFadeInFixedTime(IdleStateHash, 0.3f, 0, 0f);
        }
        public void PlayHappyIdle()
        {
            Animator.CrossFadeInFixedTime(HappyIdleStateHash, 0.15f, 0, 0f);
        }
        public void PlayMove()
        {
            Animator.CrossFadeInFixedTime(MoveStateHash, 0.15f, 0, 0f);
        }

        public void PlayCarryIdle(PlayerCarryController.CarryType carryType)
        {
            int preferred = carryType == PlayerCarryController.CarryType.File
                ? FileCarryIdleStateHash
                : carryType == PlayerCarryController.CarryType.CockroachNest
                    ? NestCarryIdleStateHash
                    : CarryIdleStateHash;
            PlayCarryState(preferred, CarryIdleStateHash);
        }

        public void PlayCarryAndMove(PlayerCarryController.CarryType carryType)
        {
            int preferred = carryType == PlayerCarryController.CarryType.File
                ? FileCarryAndMoveStateHash
                : carryType == PlayerCarryController.CarryType.CockroachNest
                    ? NestCarryAndMoveStateHash
                    : CarryAndMoveStateHash;
            PlayCarryState(preferred, CarryAndMoveStateHash);
        }

        private void PlayCarryState(int preferredHash, int fallbackHash)
        {
            if (Animator == null)
            {
                return;
            }

            int stateHash = Animator.HasState(0, preferredHash) ? preferredHash : fallbackHash;
            Animator.CrossFadeInFixedTime(stateHash, 0.15f, 0, 0f);
        }
        public void PlayDeath()
        {

        }
        public void HappyIdleEnd()
        {
            IsHappyIdleFinished = true;
        }
    }
}
