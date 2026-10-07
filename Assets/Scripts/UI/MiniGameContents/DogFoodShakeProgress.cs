using System;

namespace TapTapGameJam.UI
{
    // 仅统计有效的左右反向晃动，与 Unity 输入及 UI 显示无关。
    public sealed class DogFoodShakeProgress
    {
        private readonly float _distance;
        private readonly float _portion;
        private float _extremeX;
        private int _direction;
        private bool _tracking;

        public float Progress { get; private set; }
        public bool IsComplete => Progress >= 1f;

        public DogFoodShakeProgress(float distance, float portion)
        {
            if (distance <= 0f || float.IsNaN(distance) || float.IsInfinity(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (portion <= 0f || portion > 1f || float.IsNaN(portion))
                throw new ArgumentOutOfRangeException(nameof(portion));
            _distance = distance;
            _portion = portion;
        }

        public bool Sample(float x)
        {
            if (IsComplete) return false;
            if (!_tracking)
            {
                _tracking = true;
                _extremeX = x;
                return false;
            }

            float delta = x - _extremeX;
            if (_direction == 0)
            {
                if (Math.Abs(delta) >= _distance)
                {
                    _direction = Math.Sign(delta);
                    _extremeX = x;
                }
                return false;
            }

            // 同方向移动只更新极值，长距离单向拖动不会直接完成。
            if ((_direction > 0 && delta > 0f) || (_direction < 0 && delta < 0f))
            {
                _extremeX = x;
                return false;
            }

            if (Math.Abs(delta) < _distance) return false;
            _direction = -_direction;
            _extremeX = x;
            Progress = Math.Min(1f, Progress + _portion);
            if (Progress > 0.99999f) Progress = 1f;
            return true;
        }

        public void StopTracking()
        {
            _tracking = false;
            _direction = 0;
        }

        public void Reset()
        {
            Progress = 0f;
            StopTracking();
        }
    }
}
