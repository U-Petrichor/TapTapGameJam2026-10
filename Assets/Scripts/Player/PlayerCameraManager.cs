using UnityEngine;
using UnityEngine.Serialization;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;

namespace TapTapGameJam.Player
{
    public class PlayerCameraManager : MonoBehaviour
    {
        private const float MaxPitch = 89f;

        [SerializeField] private PlayerInputManager _inputManager;
        [SerializeField, Min(0.001f)] private float _scaleStep = 0.05f;
        [FormerlySerializedAs("_minHeight")]
        [SerializeField, Min(0.01f)] private float _minScale = 0.5f;
        [FormerlySerializedAs("_maxHeight")]
        [Tooltip("沿初始偏移直线拉远到这个倍数后，开始转向玩家头顶。")]
        [SerializeField, Min(1f)] private float _curveStartScale = 1.5f;
        [Tooltip("89 度俯视终点相对玩家的高度，必须大于曲线起点到玩家的距离。")]
        [SerializeField, Min(1f)] private float _topHeight = 40f;
        [SerializeField, Min(0.01f)] private float _smoothTime = 0.2f;
        [SerializeField, Min(0.01f)] private float _rotationSmoothTime = 0.2f;

        [SerializeField] private CinemachineFollow _follow;
        private Vector3 _initialOffset;
        private Vector3 _horizontalDirection;
        private float _initialDistance;
        private float _initialPitch;
        private float _targetScale = 1f;
        private float _currentScale = 1f;
        private float _scaleVelocity;
        private float _targetOrbitAngle;
        private float _currentOrbitAngle;
        private float _orbitVelocity;
        private TrackerSettings _originalTrackerSettings;
        private bool _initialized;

        private float MaxDistance => _topHeight / Mathf.Sin(MaxPitch * Mathf.Deg2Rad);

        private void Start()
        {
            if (_inputManager == null || _follow == null
                || _follow.GetComponent<CinemachineCamera>() == null)
            {
                Debug.LogError("[PlayerCameraManager]: 请配置 InputManager 和带 CinemachineCamera 的 Follow。", this);
                enabled = false;
                return;
            }

            _initialOffset = _follow.FollowOffset;
            _initialDistance = _initialOffset.magnitude;
            Vector3 horizontal = new Vector3(_initialOffset.x, 0f, _initialOffset.z);
            if (horizontal.sqrMagnitude < 0.0001f || _initialOffset.y <= 0f)
            {
                Debug.LogError("[PlayerCameraManager]: 初始相机偏移必须位于玩家斜上方。", this);
                enabled = false;
                return;
            }

            _horizontalDirection = horizontal.normalized;
            _initialPitch = Mathf.Atan2(_initialOffset.y, horizontal.magnitude);
            _topHeight = Mathf.Max(_topHeight, _initialDistance * _curveStartScale + 0.01f);
            _originalTrackerSettings = _follow.TrackerSettings;

            _initialized = true;
            ConfigureFollow();
            ApplyCameraPose();
        }

        private void OnEnable()
        {
            if (_inputManager != null)
            {
                _inputManager.EventOnCameraScroll += HandleOnScroll;
                _inputManager.EventOnCameraRotate += HandleOnRotate;
            }

            if (_initialized)
                ConfigureFollow();
        }

        private void OnDisable()
        {
            if (_inputManager != null)
            {
                _inputManager.EventOnCameraScroll -= HandleOnScroll;
                _inputManager.EventOnCameraRotate -= HandleOnRotate;
            }

            if (_initialized && _follow != null)
                _follow.TrackerSettings = _originalTrackerSettings;
            _scaleVelocity = 0f;
            _orbitVelocity = 0f;
        }

        private void ConfigureFollow()
        {
            // 轨迹本身已做平滑；额外的位置阻尼会使玩家偏离圆心。
            _follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            _follow.TrackerSettings.PositionDamping = Vector3.zero;
        }

        private void HandleOnScroll(int direction)
        {
            if (!_initialized || Time.timeScale <= 0f)
                return;

            _targetScale = Mathf.Clamp(
                _targetScale + direction * _scaleStep, _minScale, MaxDistance / _initialDistance);
        }

        private void HandleOnRotate(int direction)
        {
            if (!_initialized || Time.timeScale <= 0f)
                return;

            // 不用最短角度插值，连续多次按键时也保留玩家指定的旋转方向。
            _targetOrbitAngle += direction * 45f;
        }

        private void Update()
        {
            if (!_initialized || Time.deltaTime <= 0f)
                return;

            _currentScale = Mathf.SmoothDamp(
                _currentScale, _targetScale, ref _scaleVelocity, _smoothTime);
            _currentOrbitAngle = Mathf.SmoothDamp(
                _currentOrbitAngle, _targetOrbitAngle, ref _orbitVelocity, _rotationSmoothTime);

            if (Mathf.Abs(_currentScale - _targetScale) < 0.0001f)
            {
                _currentScale = _targetScale;
                _scaleVelocity = 0f;
            }
            if (Mathf.Abs(_currentOrbitAngle - _targetOrbitAngle) < 0.001f)
            {
                _targetOrbitAngle = Mathf.Repeat(_targetOrbitAngle, 360f);
                _currentOrbitAngle = _targetOrbitAngle;
                _orbitVelocity = 0f;
            }

            ApplyCameraPose();
        }

        private void ApplyCameraPose()
        {
            _follow.FollowOffset = Quaternion.AngleAxis(_currentOrbitAngle, Vector3.up)
                * EvaluateZoomOffset(_currentScale);
            // 最高 89 度，视线不会与世界向上方向共线，直接朝向玩家即可。
            _follow.transform.rotation = Quaternion.LookRotation(-_follow.FollowOffset, Vector3.up);
        }

        private Vector3 EvaluateZoomOffset(float scale)
        {
            if (scale <= _curveStartScale)
                return _initialOffset * scale;

            float distance = Mathf.Min(scale * _initialDistance, MaxDistance);
            float curveStartDistance = _curveStartScale * _initialDistance;
            float t = Mathf.InverseLerp(curveStartDistance, MaxDistance, distance);

            // 五次平滑曲线在接点的一、二阶导数均为零，平滑接上原直线。
            float blend = t * t * t * (10f + t * (-15f + 6f * t));
            float pitch = Mathf.Lerp(_initialPitch, MaxPitch * Mathf.Deg2Rad, blend);
            return _horizontalDirection * (distance * Mathf.Cos(pitch))
                + Vector3.up * (distance * Mathf.Sin(pitch));
        }
    }
}
