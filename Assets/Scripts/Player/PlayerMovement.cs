using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TapTapGameJam.Buffs;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [Tooltip("拖入实际输出画面的 Main Camera（带 Camera 和 CinemachineBrain 的物体）。移动方向按它的世界朝向计算，不要拖入 CamerachineManager 父物体。")]
        [SerializeField] private Transform _camTransform;
        [SerializeField] private PlayerBasic _player;
        [SerializeField] private float _rotationSpeed = 360f;
        [SerializeField] private float _moveSpeed = 4.5f;
        [Tooltip("冲刺时移动速度的倍率（Shift）。体力是否够由 PlayerHealth 决定")]
        [SerializeField, Min(1f)] private float _sprintSpeedMultiplier = 1.8f;

        private Vector2 _moveInput;
        private Vector2 MoveDirection;

        private PlayerBuffs _buffs;
        private bool _buffsResolved;

        /// <summary>是否处于冲刺状态。由 PlayerHealth 每帧写入（它才知道体力够不够）</summary>
        public bool IsSprinting { get; set; }

        /// <summary>
        /// 玩家身上的 Buff 宿主。<see cref="PlayerBuffs"/> 是 PlayerBasic 在 Awake 里补上的，
        /// 而同一物体上各组件 Awake 的先后顺序不保证，所以这里惰性取，取不到就下一帧再试。
        /// </summary>
        private PlayerBuffs Buffs
        {
            get
            {
                if (!_buffsResolved)
                {
                    _buffs = GetComponent<PlayerBuffs>();
                    _buffsResolved = _buffs != null;
                }

                return _buffs;
            }
        }

        /// <summary>
        /// 当前实际移动速度 = 基准 × Buff 系数（冲刺时再乘冲刺倍率）。
        /// <see cref="_moveSpeed"/> 永远只是基准值，天赋加成不写回它。
        /// </summary>
        public float CurrentMoveSpeed
        {
            get
            {
                float speed = _moveSpeed;
                PlayerBuffs buffs = Buffs;
                if (buffs != null)
                {
                    speed = buffs.Apply(BuffKey.PlayerMoveSpeedPercent, speed);
                }

                return IsSprinting ? speed * SprintMultiplier : speed;
            }
        }

        /// <summary>冲刺倍率（含「冲刺速度」Buff）</summary>
        private float SprintMultiplier
        {
            get
            {
                float multiplier = _sprintSpeedMultiplier;
                PlayerBuffs buffs = Buffs;
                if (buffs != null)
                {
                    multiplier *= buffs.GetFactor(BuffKey.SprintSpeedPercent);
                }

                return multiplier;
            }
        }

        // 由当前状态提交允许执行的移动方向。
        public void SetMoveInput(Vector2 input)
        {
            _moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void FixedUpdate()
        {
            MovePlayer();
            TryDepositAtNight();
        }
        /// <summary>
        /// 根据相机的方向确定Player移动方向
        /// </summary>
        private void MovePlayer()
        {
            if (_camTransform == null)
            {
                MoveDirection = Vector2.zero;
                Debug.LogError($"InputManager.cs:丢失相机坐标");
                _rigidbody.linearVelocity = new Vector3(0, 0, 0);
                return;
            }

            Vector3 forward = _camTransform.forward;
            Vector3 right = _camTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            MoveDirection = new Vector2(
                forward.x * _moveInput.y + right.x * _moveInput.x,
                forward.z * _moveInput.y + right.z * _moveInput.x
            );
            MoveDirection = Vector2.ClampMagnitude(MoveDirection, 1f);

            // 冲刺只改速度，不改方向；体力够不够由 PlayerHealth 写入 IsSprinting
            float speed = CurrentMoveSpeed;
            _rigidbody.linearVelocity = new Vector3(
                MoveDirection.x * speed,
                _rigidbody.linearVelocity.y,
                MoveDirection.y * speed
            );
            UpdateMoveAnimation();
        }

        public void TryDepositAtNight()
        {
            // TODO: 判断 GamePhase.Night
            // 在夜晚，移动的同时释放玩家信息素
            // 白天或初始化阶段尚未创建夜晚网格，不写信息素。
            if (GridManager.Instance == null || CockroachManager.Instance == null ||
                CockroachManager.Instance.cockroachSetting == null) return;
            if (_rigidbody.linearVelocity.magnitude > 0.1f)
            {
                CockroachSettings settings = CockroachManager.Instance.cockroachSetting;
                GridManager.Instance.WritePheromone(PheromoneKind.Player, new Vector2(transform.position.x, transform.position.z),
                    settings.MarkerMax, settings.MarkerRadius);
            }
        }


        private void UpdateMoveAnimation()
        {
            float currentSpeed = MoveDirection.magnitude;
            if (currentSpeed > 0.0001f)
            {
                Vector3 direction = new Vector3(
                    MoveDirection.x,
                    0f,
                    MoveDirection.y
                );

                Quaternion targetRotation = Quaternion.LookRotation(direction);

                Quaternion rotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);

                _rigidbody.MoveRotation(rotation);
            }
        }
    }

}
