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
        public void PlayDeath()
        {

        }
        public void HappyIdleEnd()
        {
            IsHappyIdleFinished = true;
        }
    }
}