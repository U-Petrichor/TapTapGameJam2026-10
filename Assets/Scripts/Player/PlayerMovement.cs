using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
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

        private Vector2 _moveInput;
        private Vector2 MoveDirection;


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

            _rigidbody.linearVelocity = new Vector3(
                MoveDirection.x * _moveSpeed,
                _rigidbody.linearVelocity.y,
                MoveDirection.y * _moveSpeed
            );
            UpdateMoveAnimation();
        }

        public void TryDepositAtNight()
        {
            // TODO: 判断 GamePhase.Night
            // 在夜晚，移动的同时释放玩家信息素
            Assert.IsTrue(GridManager.Instance);
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
