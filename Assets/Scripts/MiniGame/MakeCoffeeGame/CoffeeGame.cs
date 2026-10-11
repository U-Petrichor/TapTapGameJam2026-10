using System;
using TapTapGameJam.Coffee;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 三步 2D 咖啡：长按研磨机、拖粉杯到杯子、拖住热水壶注水 2 秒。
    public class CoffeeGame : MiniGameContent, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("三步咖啡器具")]
        [SerializeField] private RectTransform _grindButton;
        [SerializeField] private RectTransform _grounds;
        [SerializeField] private RectTransform _kettle;
        [SerializeField] private RectTransform _cupZone;
        [SerializeField] private RectTransform _pourZone;
        [SerializeField] private GameObject _waterEffect;
        [SerializeField] private TMP_Text _stepsText;
        [SerializeField] private Slider _progressBar;
        [Header("步骤时长（秒）")]
        [SerializeField, Min(0.1f)] private float _grindSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float _waterSeconds = 2f;

        private static readonly string[] StepNames =
            { "按住研磨机", "把咖啡粉放到杯子上", "按住热水壶浇水 2 秒" };
        private RectTransform[] _items;
        private Vector3[] _initialPositions;
        private CoffeeRecipe _recipe;
        private RectTransform _dragged;
        private Vector2 _offset;
        private Camera _eventCamera;
        private int _pointerId;
        private bool _holdingGrinder;
        private bool _running;
        private Action _onCompleted;

        public bool IsConfigured => _grindButton != null && _grounds != null && _kettle != null
            && _cupZone != null && _pourZone != null;

        public void Configure(RectTransform grindButton, RectTransform grounds, RectTransform kettle,
            RectTransform cupZone, RectTransform pourZone, GameObject waterEffect,
            TMP_Text steps, Slider progress)
        {
            _grindButton = grindButton;
            _grounds = grounds;
            _kettle = kettle;
            _cupZone = cupZone;
            _pourZone = pourZone;
            _waterEffect = waterEffect;
            _stepsText = steps;
            _progressBar = progress;
            CacheItems();
        }

        private void Awake() => CacheItems();

        private void CacheItems()
        {
            _items = new[] { _grounds, _kettle };
            _initialPositions = new Vector3[_items.Length];
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] != null) _initialPositions[i] = _items[i].localPosition;
        }

        public override void Begin(Action onCompleted)
        {
            ResetView();
            if (!IsConfigured)
                throw new InvalidOperationException("请配置研磨机、粉杯、热水壶和中央杯子区域。");

            _recipe = new CoffeeRecipe(Mathf.Max(0.1f, _grindSeconds), Mathf.Max(0.1f, _waterSeconds));
            _recipe.StepChanged += OnStepChanged;
            _onCompleted = onCompleted;
            _running = true;
            Refresh();
        }

        private bool Hit(Transform hit, RectTransform target) =>
            hit != null && target != null && (hit == target || hit.IsChildOf(target));

        public void OnPointerDown(PointerEventData evt)
        {
            if (!_running || evt.button != PointerEventData.InputButton.Left) return;
            Transform hit = evt.pointerPressRaycast.gameObject != null
                ? evt.pointerPressRaycast.gameObject.transform : null;
            if (Hit(hit, _grindButton) && _recipe.StartGrinding())
            {
                _holdingGrinder = true;
                _pointerId = evt.pointerId;
                _eventCamera = evt.pressEventCamera;
                Refresh();
            }
        }

        public void OnPointerUp(PointerEventData evt)
        {
            if (evt.pointerId != _pointerId) return;
            _holdingGrinder = false;
            _recipe?.StopGrinding();
        }

        private bool CanDrag(RectTransform item) => _running && _recipe != null &&
            ((item == _grounds && _recipe.Step == CoffeeStep.PlaceGrounds) ||
             (item == _kettle && _recipe.Step == CoffeeStep.PourWater));

        public void OnBeginDrag(PointerEventData evt)
        {
            if (!_running || _dragged != null || evt.button != PointerEventData.InputButton.Left) return;
            var hit = evt.pointerPressRaycast.gameObject;
            if (hit == null) return;
            foreach (var item in _items)
            {
                if (!Hit(hit.transform, item) || !CanDrag(item)) continue;
                if (!(item.parent is RectTransform parent) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parent, evt.position, evt.pressEventCamera, out var point)) return;
                _dragged = item;
                _pointerId = evt.pointerId;
                _eventCamera = evt.pressEventCamera;
                _offset = (Vector2)item.localPosition - point;
                item.SetAsLastSibling();
                return;
            }
        }

        public void OnDrag(PointerEventData evt)
        {
            if (!_running || _dragged == null || evt.pointerId != _pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_dragged.parent, evt.position, _eventCamera, out var point)) return;
            Rect bounds = ((RectTransform)_dragged.parent).rect;
            point += _offset;
            _dragged.localPosition = new Vector3(
                Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(point.y, bounds.yMin, bounds.yMax),
                _dragged.localPosition.z);
        }

        private bool Over(RectTransform item, RectTransform zone) =>
            RectTransformUtility.RectangleContainsScreenPoint(
                zone, RectTransformUtility.WorldToScreenPoint(_eventCamera, item.position), _eventCamera);

        public void OnEndDrag(PointerEventData evt)
        {
            if (_dragged == null || evt.pointerId != _pointerId) return;
            var item = _dragged;
            _dragged = null;
            if (_running && CanDrag(item))
            {
                if (item == _grounds && Over(item, _cupZone)) _recipe.PlaceGrounds();
                if (item == _kettle) _recipe.StopPouringWater();
            }
            ResetItem(item);
            if (_running) Refresh();
        }

        private void Update()
        {
            if (!_running || _recipe == null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                _holdingGrinder = false;
                _recipe.StopGrinding();
            }

            if (_holdingGrinder && RectTransformUtility.RectangleContainsScreenPoint(
                _grindButton, mouse.position.ReadValue(), _eventCamera))
                _recipe.Grind(Time.deltaTime);

            bool pouring = _dragged == _kettle && mouse != null && mouse.leftButton.isPressed
                && Over(_kettle, _pourZone);
            if (pouring)
            {
                _recipe.StartPouringWater();
                _recipe.PourWater(Time.deltaTime);
                SetVisible(_waterEffect, true);
            }
            else
            {
                _recipe.StopPouringWater();
                SetVisible(_waterEffect, false);
            }
            if (_running) Refresh();
        }

        private void OnStepChanged(CoffeeStep step)
        {
            _holdingGrinder = false;
            if (_dragged != null) { ResetItem(_dragged); _dragged = null; }
            if (step == CoffeeStep.Complete)
            {
                var completed = _onCompleted;
                End();
                completed?.Invoke();
            }
            else Refresh();
        }

        private void Refresh()
        {
            if (_recipe == null) return;
            int completed = (int)_recipe.Step;
            if (_stepsText != null)
            {
                var lines = new string[StepNames.Length];
                for (int i = 0; i < lines.Length; i++)
                    lines[i] = i < completed ? $"<color=#2A9D70>[完成] {i + 1}. {StepNames[i]}</color>"
                        : i == completed ? $"<b>> {i + 1}. {StepNames[i]}</b>" : $"{i + 1}. {StepNames[i]}";
                _stepsText.text = string.Join("\n", lines);
            }
            if (_progressBar != null)
                _progressBar.SetValueWithoutNotify(Mathf.Clamp01((completed + _recipe.Progress) / 3f));
            SetVisible(_grounds != null ? _grounds.gameObject : null, true);
            SetVisible(_kettle != null ? _kettle.gameObject : null, true);
        }

        private static void SetVisible(GameObject target, bool active)
        { if (target != null) target.SetActive(active); }

        private void ResetItem(RectTransform item)
        {
            if (_items == null) return;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] == item && item != null) item.localPosition = _initialPositions[i];
        }

        public override void End()
        {
            _running = _holdingGrinder = false;
            _onCompleted = null;
            if (_dragged != null) ResetItem(_dragged);
            _dragged = null;
            if (_recipe != null) _recipe.StepChanged -= OnStepChanged;
            SetVisible(_waterEffect, false);
        }

        public override void ResetView()
        {
            End();
            _recipe = null;
            if (_items != null)
                foreach (var item in _items) { ResetItem(item); SetVisible(item != null ? item.gameObject : null, true); }
            SetVisible(_waterEffect, false);
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(0f);
            if (_stepsText != null) _stepsText.text = string.Join("\n", StepNames);
        }

        private void OnDisable() => End();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                _holdingGrinder = false;
                _recipe?.StopGrinding();
                if (_dragged != null) ResetItem(_dragged);
                _dragged = null;
            }
        }
        private void OnDestroy() => End();
    }
}
