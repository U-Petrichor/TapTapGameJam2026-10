using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>NPC 行走状态：走到随机目标或超时后回到 Idle。</summary>
    public sealed class WalkState : NpcStateBase
    {
        private Vector3 _destination;
        private float _elapsedTime;
        private float _stuckTime;

        public WalkState(NPC owner) : base(owner) { }

        public override void Enter()
        {
            _destination = Owner.GetNextWalkDestination();
            _elapsedTime = 0f;
            _stuckTime = 0f;
            Owner.StartMoving(_destination);
            Owner.PlayWalkAnimation();
        }

        public override void Update()
        {
            float deltaTime = Time.deltaTime;
            _elapsedTime += deltaTime;

            bool reached = Owner.Agent != null && Owner.Agent.isOnNavMesh
                ? Owner.HasReachedDestination()
                : Owner.MoveTowards(_destination, deltaTime);
            bool invalidPath = Owner.Agent != null && Owner.Agent.isOnNavMesh &&
                               !Owner.Agent.pathPending &&
                               Owner.Agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid;

            if (Owner.Agent != null && Owner.Agent.isOnNavMesh && !Owner.Agent.pathPending)
            {
                bool hasRemainingPath = Owner.Agent.remainingDistance > Owner.Agent.stoppingDistance;
                bool isBarelyMoving = Owner.Agent.velocity.sqrMagnitude < 0.01f;
                _stuckTime = hasRemainingPath && isBarelyMoving ? _stuckTime + deltaTime : 0f;
            }

            if (reached || invalidPath || _stuckTime >= 2f || _elapsedTime >= Owner.GetMaximumWalkDuration())
            {
                if (reached)
                {
                    Owner.NotifyWalkDestinationReached();
                }
                StateMachine.ChangeState(Owner.Idle);
            }
        }

        public override void Exit()
        {
            Owner.StopMoving();
        }
    }
}
