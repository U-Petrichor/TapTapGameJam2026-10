using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace TapTapGameJam.Player
{
    public class PlayerCameraManager : MonoBehaviour
    {
        [SerializeField] private PlayerInputManager _inputManager;
        [SerializeField] private float _scaleStep = 0.05f;   // 每次滚动调整的高度
        [SerializeField] private float _minHeight = 0.5f;
        [SerializeField] private float _maxHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.2f;

        [SerializeField]private CinemachineFollow _follow;
        private Vector3 _initialOffset;
        private float _targetScale = 1f;
        private float _currentScale = 1f;
        private float _scaleVelocity;

        private void Awake()
        {
            if(_inputManager == null)
            {
                Debug.LogError("[PlayerCameraManager]: PlayerInputManager没有配置");
                enabled = false;
            }
        }
        private void Start()
        {
            if (_follow == null)
            {
                Debug.LogError("[PlayerCameraManager]: CinemachineFollow没有配置");
                enabled = false;
                return;
            }
            _initialOffset = _follow.FollowOffset;
        }
        private void OnEnable()
        {
            if (_inputManager != null)
                _inputManager.EventOnCameraScroll += HandleOnScroll;

        }
        private void OnDisable()
        {
            if (_inputManager != null)
                _inputManager.EventOnCameraScroll -= HandleOnScroll;

            _scaleVelocity = 0f;

        }
        private void HandleOnScroll(int direction)
        {
            _targetScale = Mathf.Clamp(
                _targetScale + direction * _scaleStep,
                _minHeight,
                _maxHeight
            );
        }
        private void Update()
        {
            if (Time.deltaTime <= 0f)
                return;

            _currentScale = Mathf.SmoothDamp(
                _currentScale,
                _targetScale,
                ref _scaleVelocity,
                _smoothTime
            );

            // 接近目标时收敛到准确值。
            if (Mathf.Abs(_currentScale - _targetScale) < 0.0001f)
            {
                _currentScale = _targetScale;
                _scaleVelocity = 0f;
            }

            _follow.FollowOffset = _initialOffset * _currentScale;
        }
    }
}
