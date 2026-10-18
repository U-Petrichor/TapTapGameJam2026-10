using TapTapGameJam.Audio;
using System;
using TapTapGameJam.Coffee;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // 所有判定区域、粉杯和壶嘴都独立绑定，替换图片不改变规则。
    public class CoffeeGame : MiniGameContent, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("放豆与研磨")]
        [SerializeField] private RectTransform _beans;
        [SerializeField] private RectTransform _beansZone;
        [SerializeField] private RectTransform _grindButton;
        [Tooltip("独立手柄图片，轴心设在圆形连接处。只在有效研磨时旋转。")]
        [SerializeField] private RectTransform _grinderHandle;
        [SerializeField, Min(0f)] private float _grindDegreesPerSecond = 240f;
        [Header("盛粉杯与咖啡机")]
        [SerializeField] private RectTransform _grounds;
        [SerializeField] private GameObject _groundVisual;
        [SerializeField] private GameObject _placedGrounds;
        [SerializeField] private RectTransform _cupZone;
        [Header("热水壶与注水")]
        [SerializeField] private RectTransform _kettle;
        [SerializeField] private RectTransform _kettleSpout;
        [SerializeField] private RectTransform _pourZone;
        [Tooltip("滤杯接水点。水流从倾倒后的壶嘴连接到这里。")]
        [SerializeField] private RectTransform _waterLandingPoint;
        [SerializeField] private GameObject _waterEffect;
        [Tooltip("滤杯下方流入咖啡杯的咖啡液，和有效注水同步启停。")]
        [SerializeField] private GameObject _coffeeEffect;
        [SerializeField] private float _kettlePourAngle = 25f;
        [Header("阶段和进度")]
        [SerializeField] private Image[] _stageImages = new Image[5];
        [SerializeField] private TMP_Text _stepsText;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private Color _currentStageColor = Color.white;
        [SerializeField] private Color _finishedStageColor = new Color(0.65f, 0.9f, 0.7f, 1f);
        [SerializeField] private Color _futureStageColor = new Color(0.65f, 0.65f, 0.65f, 0.45f);
        [Header("步骤时长（秒）")]
        [SerializeField, Min(0.1f)] private float _grindSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float _waterSeconds = 2f;
        [SerializeField, Min(0f)] private float _completionDelay = 0.45f;

        private static readonly string[] StepNames =
        {
            "将咖啡豆拖入研磨机后松手", "长按研磨机，松手可暂停", "将盛粉杯拖到咖啡机滤杯后松手",
            "按住热水壶，将壶嘴移到滤杯上方注水", "咖啡制作完成！"
        };
        private RectTransform[] _items;
        private Vector3[] _initialPositions;
        private Quaternion[] _initialRotations;
        private int[] _initialSiblingIndices;
        private CoffeeRecipe _recipe;
        private RectTransform _dragged;
        private CanvasGroup _panelGroup;
        private Vector2 _offset;
        private Camera _eventCamera;
        private int _pointerId;
        private bool _holdingGrinder, _running;
        private int _lastGrindingFrame = -1;
        private float _completionRemaining = -1f;
        private Action _onCompleted;
        private Quaternion _initialHandleRotation;
        private bool _hasInitialHandleRotation;

        public bool IsConfigured => _beans != null && _beansZone != null && _grindButton != null
            && _grounds != null && _groundVisual != null && _kettle != null && _kettleSpout != null
            && _cupZone != null && _pourZone != null && _waterLandingPoint != null;
        private bool CanInteract => _running && (_panelGroup == null || _panelGroup.interactable);
        private void Awake() => CacheItems();
        private void CacheItems()
        {
            _items = new[] { _beans, _grounds, _kettle };
            _initialPositions = new Vector3[_items.Length];
            _initialRotations = new Quaternion[_items.Length];
            _initialSiblingIndices = new int[_items.Length];
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null) continue;
                _initialPositions[i] = _items[i].localPosition;
                _initialRotations[i] = _items[i].localRotation;
                _initialSiblingIndices[i] = _items[i].GetSiblingIndex();
            }
        }
        public override void Begin(Action onCompleted)
        {
            if (_items == null) CacheItems();
            if (_grinderHandle != null && !_hasInitialHandleRotation)
            {
                _initialHandleRotation = _grinderHandle.localRotation;
                _hasInitialHandleRotation = true;
            }
            ResetView();
            if (!IsConfigured) throw new InvalidOperationException("请绑定咖啡豆、放豆区域、研磨机、盛粉杯与粉堆、热水壶与壶嘴、放粉及注水区域、滤杯接水点。");
            _panelGroup = GetComponentInParent<MiniGamePanelUI>()?.GetComponent<CanvasGroup>();
            _recipe = new CoffeeRecipe(Mathf.Max(0.1f, _grindSeconds), Mathf.Max(0.1f, _waterSeconds));
            _recipe.StepChanged += OnStepChanged;
            _onCompleted = onCompleted;
            _running = true;
            Refresh();
        }
        private static bool Hit(Transform hit, RectTransform target) =>
            hit != null && target != null && (hit == target || hit.IsChildOf(target));
        public void OnPointerDown(PointerEventData evt)
        {
            if (!CanInteract || _holdingGrinder || _dragged != null || evt.button != PointerEventData.InputButton.Left) return;
            Transform hit = evt.pointerPressRaycast.gameObject != null ? evt.pointerPressRaycast.gameObject.transform : null;
            if (!Hit(hit, _grindButton) || !_recipe.StartGrinding()) return;
            _holdingGrinder = true;
            _pointerId = evt.pointerId;
            _eventCamera = evt.pressEventCamera;
        }
        public void OnPointerUp(PointerEventData evt)
        {
            if (evt.pointerId != _pointerId) return;
            _holdingGrinder = false;
            _recipe?.StopGrinding();
        }
        private bool CanDrag(RectTransform item) => CanInteract && _recipe != null &&
            ((item == _beans && _recipe.Step == CoffeeStep.LoadBeans)
            || (item == _grounds && _recipe.Step == CoffeeStep.PlaceGrounds)
            || (item == _kettle && _recipe.Step == CoffeeStep.PourWater));
        public void OnBeginDrag(PointerEventData evt)
        {
            if (!CanInteract || _dragged != null || _holdingGrinder || evt.button != PointerEventData.InputButton.Left) return;
            var hit = evt.pointerPressRaycast.gameObject;
            if (hit == null) return;
            foreach (var item in _items)
            {
                if (!Hit(hit.transform, item) || !CanDrag(item)) continue;
                if (!(item.parent is RectTransform parent) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, evt.position, evt.pressEventCamera, out var point)) return;
                if (item == _beans || item == _grounds) Sfx.Play(SoundId.PaperBag, this);
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
            if (!CanInteract || _dragged == null || evt.pointerId != _pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_dragged.parent,
                evt.position, _eventCamera, out var point)) return;
            point += _offset;
            _dragged.localPosition = new Vector3(point.x, point.y, _dragged.localPosition.z);
        }
        private bool Over(RectTransform point, RectTransform zone) => RectTransformUtility.RectangleContainsScreenPoint(
            zone, RectTransformUtility.WorldToScreenPoint(_eventCamera, point.position), _eventCamera);
        public void OnEndDrag(PointerEventData evt)
        {
            if (_dragged == null || evt.pointerId != _pointerId) return;
            var item = _dragged;
            _dragged = null;
            if (CanDrag(item))
            {
                if (item == _beans && Over(item, _beansZone)) _recipe.LoadBeans();
                else if (item == _grounds && Over(item, _cupZone)) _recipe.PlaceGrounds();
                else if (item == _kettle) _recipe.StopPouringWater();
            }
            ResetItem(item);
            SetPourEffects(false);
            if (_running) Refresh();
        }
        private void Update()
        {
            if (_completionRemaining >= 0f)
            {
                _completionRemaining -= Time.unscaledDeltaTime;
                if (_completionRemaining <= 0f)
                {
                    var completed = _onCompleted;
                    End();
                    completed?.Invoke();
                }
                return;
            }
            if (!CanInteract || _recipe == null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed)
            {
                // 松手的落点交给 OnEndDrag 判定，不能因 Update 执行顺序提前清空拖拽物体。
                _holdingGrinder = false;
                _recipe.StopGrinding();
                _recipe.StopPouringWater();
                SetPourEffects(false);
                Refresh();
                return;
            }
            if (_holdingGrinder && RectTransformUtility.RectangleContainsScreenPoint(
                _grindButton, mouse.position.ReadValue(), _eventCamera)) AdvanceGrinding(Time.unscaledDeltaTime);
            if (!_running) return;
            bool pouring = false;
            if (_dragged == _kettle)
            {
                // 使用倾倒姿态的壶嘴判定，保持不动也能持续注水。
                _kettle.localRotation = InitialRotation(_kettle) * Quaternion.Euler(0, 0, _kettlePourAngle);
                pouring = Over(_kettleSpout, _pourZone) && IsSpoutAboveFilter();
                if (!pouring) _kettle.localRotation = InitialRotation(_kettle);
            }
            if (pouring)
            {
                _recipe.StartPouringWater();
                _recipe.PourWater(Time.unscaledDeltaTime);
            }
            else _recipe.StopPouringWater();
            SetPourEffects(_running && pouring);
            if (_running) Refresh();
        }
        private void LateUpdate()
        {
            if (_waterEffect != null && _waterEffect.activeSelf) UpdateWaterStream();
            bool processing = CanInteract && _recipe != null && _recipe.IsProcessing;
            ActionSound.SetPlaying(SoundId.Grinding, processing && _recipe.Step == CoffeeStep.Grind
                && _lastGrindingFrame == Time.frameCount);
            ActionSound.SetPlaying(SoundId.PourWater, processing && _recipe.Step == CoffeeStep.PourWater);
        }

        private void AdvanceGrinding(float deltaTime)
        {
            if (!CanInteract || !_holdingGrinder || _recipe == null ||
                _recipe.Step != CoffeeStep.Grind || !_recipe.IsProcessing || deltaTime <= 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            // 最后一帧只旋转剩余研磨时间，之后的步骤不会继续带动手柄。
            float consumed = Mathf.Min(deltaTime, (1f - _recipe.Progress) * Mathf.Max(0.1f, _grindSeconds));
            if (_grinderHandle != null)
                _grinderHandle.Rotate(0f, 0f, -Mathf.Max(0f, _grindDegreesPerSecond) * consumed, Space.Self);
            _lastGrindingFrame = Time.frameCount;
            _recipe.Grind(deltaTime);
        }
        private void OnStepChanged(CoffeeStep step)
        {
            ReleaseInput();
            Refresh();
            if (step != CoffeeStep.Complete) return;
            _running = false;
            _completionRemaining = Mathf.Max(0f, _completionDelay);
        }
        private void Refresh()
        {
            CoffeeStep step = _recipe?.Step ?? CoffeeStep.LoadBeans;
            int index = (int)step;
            if (_stepsText != null)
                _stepsText.text = StepNames[index] + ((step == CoffeeStep.Grind || step == CoffeeStep.PourWater)
                    ? $"  {Mathf.RoundToInt((_recipe?.Progress ?? 0f) * 100f)}%" : "");
            if (_progressBar != null) _progressBar.SetValueWithoutNotify(Mathf.Clamp01((index + (_recipe?.Progress ?? 0f)) / 4f));
            if (_stageImages != null)
                for (int i = 0; i < _stageImages.Length; i++)
                    if (_stageImages[i] != null) _stageImages[i].color = i < index ? _finishedStageColor : i == index ? _currentStageColor : _futureStageColor;
            SetVisible(_beans != null ? _beans.gameObject : null, step == CoffeeStep.LoadBeans);
            SetVisible(_groundVisual, step == CoffeeStep.PlaceGrounds);
            SetVisible(_placedGrounds, step == CoffeeStep.PourWater || step == CoffeeStep.Complete);
        }
        private static void SetVisible(GameObject item, bool active) { if (item != null) item.SetActive(active); }
        private void SetPourEffects(bool active)
        {
            if (active) UpdateWaterStream();
            SetVisible(_waterEffect, active);
            SetVisible(_coffeeEffect, active);
        }
        private bool IsSpoutAboveFilter() => _waterLandingPoint != null &&
            _waterLandingPoint.InverseTransformPoint(_kettleSpout.position).y > 0f;

        private void UpdateWaterStream()
        {
            if (_waterEffect == null || _kettleSpout == null || _waterLandingPoint == null
                || !(_waterEffect.transform is RectTransform stream) || stream.parent == null) return;
            // 在水流父物体的坐标系内连接两端，兼容 Canvas 缩放及壶身倾斜。
            Vector3 start = stream.parent.InverseTransformPoint(_kettleSpout.position);
            Vector3 end = stream.parent.InverseTransformPoint(_waterLandingPoint.position);
            Vector2 direction = end - start;
            stream.pivot = new Vector2(0.5f, 1f);
            stream.localPosition = start;
            stream.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
            stream.sizeDelta = new Vector2(stream.sizeDelta.x, direction.magnitude);
        }
        private Quaternion InitialRotation(RectTransform item)
        {
            for (int i = 0; i < _items.Length; i++) if (_items[i] == item) return _initialRotations[i];
            return Quaternion.identity;
        }
        private void ResetItem(RectTransform item)
        {
            if (_items == null || item == null) return;
            for (int i = 0; i < _items.Length; i++)
                if (_items[i] == item)
                {
                    item.localPosition = _initialPositions[i];
                    item.localRotation = _initialRotations[i];
                    item.SetSiblingIndex(_initialSiblingIndices[i]);
                    return;
                }
        }
        private void ReleaseInput()
        {
            _holdingGrinder = false;
            _recipe?.StopGrinding();
            _recipe?.StopPouringWater();
            if (_dragged != null) ResetItem(_dragged);
            _dragged = null;
            SetPourEffects(false);
        }
        public override void End()
        {
            ActionSound.StopAll();
            ReleaseInput();
            _running = false;
            _completionRemaining = -1f;
            _onCompleted = null;
            if (_recipe != null) { _recipe.StepChanged -= OnStepChanged; _recipe.Stop(); }
        }
        public override void ResetView()
        {
            End();
            _recipe = null;
            if (_grinderHandle != null && _hasInitialHandleRotation)
                _grinderHandle.localRotation = _initialHandleRotation;
            if (_items != null) foreach (var item in _items) ResetItem(item);
            Refresh();
        }
        private void OnDisable() => End();
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ReleaseInput(); }
        private void OnDestroy() => End();
    }
}
