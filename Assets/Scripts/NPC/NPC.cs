using UnityEngine;
using UnityEngine.AI;

namespace TapTapGameJam.NPC
{
    /// <summary>
    /// NPC 的基础运行组件，负责状态机、Idle/Walk 参数和简单的 XZ 平面移动。
    /// Animator Controller 的基础层需要包含名为 Idle 和 Walk 的状态。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NPC : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Animator _animator;
        [SerializeField] private NavMeshAgent _agent;

        [Header("待机")]
        [Min(0f)] [SerializeField] private float _idleDurationMin = 2f;
        [Min(0f)] [SerializeField] private float _idleDurationMax = 5f;

        [Header("行走")]
        [Min(0f)] [SerializeField] private float _walkSpeed = 1.2f;
        [Min(0f)] [SerializeField] private float _wanderRadius = 4f;
        [Tooltip("优先从这些点中选择目标；不配置时会从巡逻区域或整张 NavMesh 采样")]
        [SerializeField] private Transform[] _patrolPoints;
        [Tooltip("可选的巡逻范围。留空时使用整张 NavMesh")]
        [SerializeField] private BoxCollider _patrolArea;
        [Min(0f)] [SerializeField] private float _minimumDestinationDistance = 6f;
        [Min(1f)] [SerializeField] private float _maximumWalkDuration = 60f;
        [Min(0f)] [SerializeField] private float _stoppingDistance = 0.08f;
        [Min(0f)] [SerializeField] private float _turnSpeed = 360f;

        [Header("启动")]
        [SerializeField] private bool _startInIdle = true;

        private Vector3 _homePosition;

        public NpcStateMachine StateMachine { get; private set; }
        public IdleState Idle { get; private set; }
        public WalkState Walk { get; private set; }
        public Vector3 HomePosition => _homePosition;

        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Walk");

        protected virtual void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();

            _homePosition = transform.position;
            NormalizeSettings();

            StateMachine = new NpcStateMachine();
            Idle = new IdleState(this);
            Walk = new WalkState(this);
            OnNpcAwake();
        }

        protected virtual void Start()
        {
            if (_startInIdle)
            {
                StateMachine.ChangeState(Idle);
            }
            else
            {
                StateMachine.ChangeState(Walk);
            }
        }

        protected virtual void Update()
        {
            StateMachine?.Update();
        }

        public NavMeshAgent Agent => _agent;

        public float GetNextIdleDuration()
        {
            return Random.Range(_idleDurationMin, _idleDurationMax);
        }

        /// <summary>
        /// 取得下一個移動目標。角色 NPC 可以覆寫此方法加入自己的行為，
        /// 但沒有特殊行為時仍使用這裡的普通巡邏邏輯。
        /// </summary>
        public virtual Vector3 GetNextWalkDestination()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return GetFallbackDestination();
            }

            int attempts = 16;
            bool usingPatrolPoints = _patrolPoints != null && _patrolPoints.Length > 0;
            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate;

                if (_patrolPoints != null && _patrolPoints.Length > 0)
                {
                    Transform point = _patrolPoints[Random.Range(0, _patrolPoints.Length)];
                    if (point == null) continue;
                    candidate = point.position;
                }
                else if (_patrolArea != null)
                {
                    Bounds bounds = _patrolArea.bounds;
                    candidate = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        bounds.center.y,
                        Random.Range(bounds.min.z, bounds.max.z));
                }
                else
                {
                    candidate = GetRandomNavMeshPoint();
                }

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, _agent.areaMask) &&
                    (usingPatrolPoints || (hit.position - transform.position).sqrMagnitude >=
                    _minimumDestinationDistance * _minimumDestinationDistance))
                {
                    return hit.position;
                }
            }

            return GetFallbackDestination();
        }

        public virtual void NotifyWalkDestinationReached()
        {
        }

        /// <summary>给派生 NPC 的初始化钩子。基础 Awake 已完成后才会调用。</summary>
        protected virtual void OnNpcAwake() { }

        public float GetMaximumWalkDuration()
        {
            return _maximumWalkDuration;
        }

        private Vector3 GetFallbackDestination()
        {
            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            return new Vector3(_homePosition.x + offset.x, _homePosition.y, _homePosition.z + offset.y);
        }

        private Vector3 GetRandomNavMeshPoint()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length < 3 ||
                triangulation.indices == null || triangulation.indices.Length < 3)
            {
                return transform.position;
            }

            int triangleCount = triangulation.indices.Length / 3;
            int triangle = Random.Range(0, triangleCount);
            Vector3 a = triangulation.vertices[triangulation.indices[triangle * 3]];
            Vector3 b = triangulation.vertices[triangulation.indices[triangle * 3 + 1]];
            Vector3 c = triangulation.vertices[triangulation.indices[triangle * 3 + 2]];

            // 均匀采样三角形内部，避免只在顶点附近选点。
            float r1 = Mathf.Sqrt(Random.value);
            float r2 = Random.value;
            return a * (1f - r1) + b * (r1 * (1f - r2)) + c * (r1 * r2);
        }

        public void StartMoving(Vector3 destination)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        public bool HasReachedDestination()
        {
            if (_agent == null || !_agent.isOnNavMesh) return false;
            if (_agent.pathPending) return false;
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return false;

            return _agent.remainingDistance <= _agent.stoppingDistance + _stoppingDistance;
        }

        /// <summary>向目标移动；返回 true 表示已到达目标。</summary>
        public bool MoveTowards(Vector3 destination, float deltaTime)
        {
            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;

            float distance = toDestination.magnitude;
            if (distance <= _stoppingDistance) return true;

            Vector3 direction = toDestination / distance;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * deltaTime);
            }

            float distanceThisFrame = Mathf.Min(_walkSpeed * deltaTime, distance);
            Vector3 movement = direction * distanceThisFrame;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Move(movement);
            }
            else
            {
                transform.position += movement;
            }

            return distance <= _stoppingDistance + distanceThisFrame;
        }

        public void StopMoving()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public void PlayIdleAnimation() => PlayAnimation(IdleStateHash);

        public void PlayWalkAnimation() => PlayAnimation(WalkStateHash);

        private void PlayAnimation(int stateHash)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;

            if (_animator.HasState(0, stateHash))
            {
                _animator.CrossFadeInFixedTime(stateHash, 0.15f, 0, 0f);
            }
            else
            {
                Debug.LogWarning("[NPC] Animator Controller 中找不到 Idle/Walk 状态。", this);
            }
        }

        private void NormalizeSettings()
        {
            _idleDurationMin = Mathf.Max(0f, _idleDurationMin);
            _idleDurationMax = Mathf.Max(_idleDurationMin, _idleDurationMax);
            _walkSpeed = Mathf.Max(0f, _walkSpeed);
            _wanderRadius = Mathf.Max(0f, _wanderRadius);
            _minimumDestinationDistance = Mathf.Max(0f, _minimumDestinationDistance);
            _maximumWalkDuration = Mathf.Max(1f, _maximumWalkDuration);
            _stoppingDistance = Mathf.Max(0.001f, _stoppingDistance);
            _turnSpeed = Mathf.Max(0f, _turnSpeed);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? _homePosition : transform.position;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawWireSphere(center, _wanderRadius);

            if (_patrolArea != null)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
                Gizmos.DrawWireCube(_patrolArea.bounds.center, _patrolArea.bounds.size);
            }

            if (_patrolPoints == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            for (int i = 0; i < _patrolPoints.Length; i++)
            {
                if (_patrolPoints[i] != null) Gizmos.DrawSphere(_patrolPoints[i].position, 0.2f);
            }
        }
    }
}
