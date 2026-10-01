using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
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
            // UpdateMoveAnimation();
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
