using System;
using TapTapGameJam.Coffee;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class CoffeeGame : MiniGameContent, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("2D 器具与目标区域")]
        [SerializeField] private Button _heatButton;
        [SerializeField] private RectTransform _grindButton;
        [SerializeField] private RectTransform _beans, _dripper, _filter, _grounds, _kettle;
        [SerializeField] private RectTransform _grinderZone, _cupZone, _pourZone;
        [SerializeField] private GameObject _placedDripper, _placedFilter, _waterEffect;
        [SerializeField] private TMP_Text _stepsText;
        [SerializeField] private Slider _progressBar;
        [Header("步骤时长（秒）")]
        [SerializeField, Min(0.1f)] private float _heatSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _grindSeconds = 1f;
        [SerializeField, Min(0.1f)] private float _groundsSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float _waterSeconds = 2f;
        [SerializeField, Min(0.1f)] private float _dripSeconds = 0.5f;
        private static readonly string[] StepNames =
            { "点击开关烧水", "拖咖啡豆到研磨机", "按住研磨按钮", "拖滤杯到咖啡杯", "拖滤纸到滤杯", "拖粉杯到滤杯倒粉", "按住水壶在滤杯上方注水", "等待滴滤" };
        private RectTransform[] _items;
        private Vector3[] _initialPositions;
        private CoffeeRecipe _recipe;
        private RectTransform _dragged;
        private Vector2 _offset;
        private Camera _eventCamera;
        private int _pointerId;
        private bool _holdingGrinder, _running;
        private Action _onCompleted;

        public bool IsConfigured => _beans != null && _heatButton != null;
        public void Configure(Button heat, RectTransform grind, RectTransform beans, RectTransform dripper,
            RectTransform filter, RectTransform grounds, RectTransform kettle, RectTransform grinderZone,
            RectTransform cupZone, RectTransform pourZone, GameObject placedDripper, GameObject placedFilter,
            GameObject waterEffect, TMP_Text steps, Slider progress)
        {
            _heatButton = heat; _grindButton = grind; _beans = beans; _dripper = dripper; _filter = filter;
            _grounds = grounds; _kettle = kettle; _grinderZone = grinderZone; _cupZone = cupZone; _pourZone = pourZone;
            _placedDripper = placedDripper; _placedFilter = placedFilter; _waterEffect = waterEffect;
            _stepsText = steps; _progressBar = progress;
        }

        private void Awake()
        {
            _items = new[] { _beans, _dripper, _filter, _grounds, _kettle };
            _initialPositions = new Vector3[_items.Length];
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] != null) _initialPositions[i] = _items[i].localPosition;
            if (_heatButton != null) _heatButton.onClick.AddListener(Heat);
        }
        public override void Begin(Action onCompleted)
        {
            ResetView();
            foreach (var item in _items)
                if (item == null) throw new InvalidOperationException("请配置咖啡面板内的全部五个器具。");
            if (_grinderZone == null || _cupZone == null || _pourZone == null || _grindButton == null || _heatButton == null)
                throw new InvalidOperationException("请配置咖啡目标区域和操作按钮。");
            _recipe = new CoffeeRecipe(_heatSeconds, _grindSeconds, _groundsSeconds, _waterSeconds, _dripSeconds);
            _recipe.StepChanged += OnStepChanged;
            _onCompleted = onCompleted;
            _running = true;
            Refresh();
        }
        private void Heat() { if (_running) { _recipe.StartHeating(); Refresh(); } }
        private bool Hit(Transform hit, RectTransform target) => hit != null && target != null && (hit == target || hit.IsChildOf(target));
        public void OnPointerDown(PointerEventData evt)
        {
            if (!_running || evt.button != PointerEventData.InputButton.Left) return;
            if (Hit(evt.pointerPressRaycast.gameObject != null ? evt.pointerPressRaycast.gameObject.transform : null, _grindButton)
                && _recipe.Step == CoffeeStep.GrindBeans)
            {
                _holdingGrinder = true;
                _pointerId = evt.pointerId;
                _eventCamera = evt.pressEventCamera;
            }
        }
        public void OnPointerUp(PointerEventData evt) { if (evt.pointerId == _pointerId) _holdingGrinder = false; }
        private bool CanDrag(RectTransform item)
        {
            if (!_running || _recipe == null) return false;
            return (item == _beans && _recipe.Step == CoffeeStep.LoadBeans)
                || (item == _dripper && _recipe.Step == CoffeeStep.PlaceDripper)
                || (item == _filter && _recipe.Step == CoffeeStep.PlaceFilter)
                || (item == _grounds && _recipe.Step == CoffeeStep.PourGrounds && !_recipe.IsProcessing)
                || (item == _kettle && _recipe.Step == CoffeeStep.PourWater);
        }
        public void OnBeginDrag(PointerEventData evt)
        {
            if (!_running || _dragged != null || evt.button != PointerEventData.InputButton.Left) return;
            var hit = evt.pointerPressRaycast.gameObject;
            if (hit == null) return;
            foreach (var item in _items)
            {
                if (!Hit(hit.transform, item) || !CanDrag(item)) continue;
                if (!(item.parent is RectTransform parent) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
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
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragged.parent,
                evt.position, _eventCamera, out var point)) return;
            Rect bounds = ((RectTransform)_dragged.parent).rect;
            point += _offset;
            _dragged.localPosition = new Vector3(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(point.y, bounds.yMin, bounds.yMax), _dragged.localPosition.z);
        }
        private bool Over(RectTransform item, RectTransform zone) => RectTransformUtility.RectangleContainsScreenPoint(
            zone, RectTransformUtility.WorldToScreenPoint(_eventCamera, item.position), _eventCamera);
        public void OnEndDrag(PointerEventData evt)
        {
            if (_dragged == null || evt.pointerId != _pointerId) return;
            var item = _dragged;
            _dragged = null;
            if (_running && CanDrag(item))
            {
                if (item == _beans && Over(item, _grinderZone)) _recipe.LoadBeans();
                else if (item == _dripper && Over(item, _cupZone)) _recipe.PlaceDripper();
                else if (item == _filter && Over(item, _cupZone)) _recipe.PlaceFilter();
                else if (item == _grounds && Over(item, _pourZone)) _recipe.StartPouringGrounds();
            }
            ResetItem(item);
            if (_running) Refresh();
        }
        private void Update()
        {
            if (!_running || _recipe == null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) _holdingGrinder = false;
            if (_holdingGrinder && RectTransformUtility.RectangleContainsScreenPoint(
                _grindButton, mouse.position.ReadValue(), _eventCamera)) _recipe.Grind(Time.deltaTime);
            if (_dragged == _kettle && mouse != null && mouse.leftButton.isPressed && Over(_kettle, _pourZone))
            {
                SetVisible(_waterEffect, true);
                _recipe.PourWater(Time.deltaTime);
            }
            else SetVisible(_waterEffect, false);
            if (_running) _recipe.Tick(Time.deltaTime);
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
            int step = (int)_recipe.Step;
            if (_stepsText != null)
            {
                var lines = new string[StepNames.Length];
                for (int i = 0; i < lines.Length; i++)
                    lines[i] = i < step ? $"<color=#2A9D70>[完成] {i + 1}. {StepNames[i]}</color>"
                        : i == step ? $"<b>> {i + 1}. {StepNames[i]}</b>" : $"{i + 1}. {StepNames[i]}";
                _stepsText.text = string.Join("\n", lines);
            }
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(Mathf.Clamp01((step + _recipe.Progress) / 8f));
            if (_heatButton != null) _heatButton.interactable = _running && _recipe.Step == CoffeeStep.HeatWater && !_recipe.IsProcessing;
            SetVisible(_beans.gameObject, step <= (int)CoffeeStep.LoadBeans);
            SetVisible(_dripper.gameObject, step <= (int)CoffeeStep.PlaceDripper);
            SetVisible(_filter.gameObject, step <= (int)CoffeeStep.PlaceFilter);
            SetVisible(_grounds.gameObject, step >= (int)CoffeeStep.PourGrounds && step <= (int)CoffeeStep.PourWater);
            SetVisible(_placedDripper, step > (int)CoffeeStep.PlaceDripper);
            SetVisible(_placedFilter, step > (int)CoffeeStep.PlaceFilter);
        }
        private static void SetVisible(GameObject target, bool active) { if (target != null) target.SetActive(active); }
        private void ResetItem(RectTransform item)
        {
            if (_items == null) return;
            for (int i = 0; i < _items.Length; i++) if (_items[i] == item && item != null) item.localPosition = _initialPositions[i];
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
            if (_items != null) foreach (var item in _items) { ResetItem(item); if (item != null) item.gameObject.SetActive(true); }
            SetVisible(_placedDripper, false);
            SetVisible(_placedFilter, false);
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(0f);
            if (_stepsText != null) _stepsText.text = string.Join("\n", StepNames);
        }
        private void OnDisable() => End();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { _holdingGrinder = false; if (_dragged != null) ResetItem(_dragged); _dragged = null; }
        }
        private void OnDestroy() { End(); if (_heatButton != null) _heatButton.onClick.RemoveListener(Heat); }
    }
}
