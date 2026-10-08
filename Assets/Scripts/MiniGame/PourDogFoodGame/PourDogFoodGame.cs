using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 挂在小游戏内容根物体上，子 Image 的拖拽事件向上交给本组件。
    public class PourDogFoodGame : MiniGameContent,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI 绑定")]
        [SerializeField] private RectTransform _foodBag;
        [SerializeField] private RectTransform _pourOrigin;
        [SerializeField] private RectTransform _pourArea;
        [SerializeField] private Image _foodFill;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private GameObject _pourEffect;

        [Header("玩法参数")]
        [Tooltip("一次有效反向晃动需要的横向距离，使用袋子父物体的 UI 单位。")]
        [SerializeField, Min(1f)] private float _shakeDistance = 20f;
        [Tooltip("每次有效反向晃动增加多少进度；0.1 表示大约十次晃动。")]
        [SerializeField, Range(0.01f, 1f)] private float _foodPerShake = 0.1f;
        [SerializeField] private float _pourAngle = -25f;
        [SerializeField, Min(0f)] private float _effectDuration = 0.12f;

        private RectTransform _bagParent;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector2 _grabOffset;
        private Camera _eventCamera;
        private Action _onCompleted;
        private DogFoodShakeProgress _shake;
        private bool _initialized;
        private bool _running;
        private bool _dragging;
        private int _pointerId;
        private float _effectRemaining;

        private bool Initialize()
        {
            if (_initialized) return true;
            if (_foodBag == null || _pourOrigin == null || _pourArea == null
                || !(_foodBag.parent is RectTransform parent)
                || !_pourOrigin.IsChildOf(_foodBag))
            {
                Debug.LogError("[PourDogFoodGame] 请绑定狗粮袋、袋口及盆上方的接粮区域；袋口必须是袋子的子物体。", this);
                return false;
            }

            _bagParent = parent;
            _initialPosition = _foodBag.localPosition;
            _initialRotation = _foodBag.localRotation;
            _initialized = true;
            return true;
        }

        public override void Begin(Action onCompleted)
        {
            if (!Initialize()) return;
            ResetView();
            _shake = new DogFoodShakeProgress(_shakeDistance, _foodPerShake);
            _onCompleted = onCompleted;
            _running = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_running || _dragging
                || eventData.button != PointerEventData.InputButton.Left) return;

            var hit = eventData.pointerPressRaycast.gameObject;
            if (hit == null || !hit.transform.IsChildOf(_foodBag)) return;
            if (!TryGetPointer(eventData, out Vector2 pointer)) return;

            _eventCamera = eventData.pressEventCamera;
            _pointerId = eventData.pointerId;
            _grabOffset = (Vector2)_foodBag.localPosition - pointer;
            _dragging = true;
            _shake.StopTracking();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_running || !_dragging || eventData.pointerId != _pointerId) return;
            if (!TryGetPointer(eventData, out Vector2 pointer)) return;

            Vector2 position = pointer + _grabOffset;
            Rect bounds = _bagParent.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            _foodBag.localPosition = new Vector3(position.x, position.y, _initialPosition.z);

            // 使用倾倒姿态下的袋口判断是否对准盆上方的区域。
            _foodBag.localRotation = _initialRotation * Quaternion.Euler(0f, 0f, _pourAngle);
            Vector2 mouthScreen = RectTransformUtility.WorldToScreenPoint(_eventCamera, _pourOrigin.position);
            bool canPour = RectTransformUtility.RectangleContainsScreenPoint(_pourArea, mouthScreen, _eventCamera);
            if (!canPour)
            {
                _foodBag.localRotation = _initialRotation;
                _shake.StopTracking();
                StopEffect();
                return;
            }

            if (!_shake.Sample(position.x)) return;
            UpdateProgress();
            _effectRemaining = _effectDuration;
            if (_pourEffect != null) _pourEffect.SetActive(_effectDuration > 0f);

            if (_shake.IsComplete)
            {
                Action completed = _onCompleted;
                End();
                completed?.Invoke();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _pointerId) return;
            ReleaseBag();
        }

        private bool TryGetPointer(PointerEventData eventData, out Vector2 pointer)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _bagParent, eventData.position, eventData.pressEventCamera, out pointer);
        }

        private void Update()
        {
            if (_effectRemaining <= 0f) return;
            _effectRemaining -= Time.unscaledDeltaTime;
            if (_effectRemaining <= 0f) StopEffect();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseBag();
        }

        private void OnDisable() => End();

        private void ReleaseBag()
        {
            _dragging = false;
            _shake?.StopTracking();
            StopEffect();
            if (_initialized)
            {
                _foodBag.localPosition = _initialPosition;
                _foodBag.localRotation = _initialRotation;
            }
        }

        public override void End()
        {
            _running = false;
            _onCompleted = null;
            ReleaseBag();
        }

        public override void ResetView()
        {
            End();
            if (!Initialize()) return;
            _shake?.Reset();
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            float progress = _shake?.Progress ?? 0f;
            if (_foodFill != null) _foodFill.fillAmount = progress;
            if (_progressBar != null) _progressBar.normalizedValue = progress;
        }

        private void StopEffect()
        {
            _effectRemaining = 0f;
            if (_pourEffect != null) _pourEffect.SetActive(false);
        }
    }

}
