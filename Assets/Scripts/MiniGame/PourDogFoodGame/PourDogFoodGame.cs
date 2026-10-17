using TapTapGameJam.Audio;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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
        [Tooltip("狗粮生成父物体；矩形下边和左右边作为碗的碰撞边界，上边为碗口。")]
        [SerializeField] private RectTransform _foodContent;
        [SerializeField] private Sprite[] _foodSprites = new Sprite[3];
        [SerializeField] private Image _foodFill;
        [SerializeField] private Slider _progressBar;
        [SerializeField] private GameObject _pourEffect;

        [Header("玩法参数")]
        [SerializeField, Min(1)] private int _targetFoodCount = 100;
        [SerializeField, Min(1f)] private float _foodsPerSecond = 15f;
        [SerializeField] private float _pourAngle = -25f;
        [Header("颗粒表现（FoodContent 的 UI 单位）")]
        [SerializeField, Min(1f)] private float _foodSize = 16f;
        [SerializeField, Range(0f, 0.5f)] private float _sizeVariation = 0.2f;
        [SerializeField, Min(0.05f)] private float _fallDuration = 0.45f;
        [SerializeField, Min(0f)] private float _mouthSpread = 5f;
        [Tooltip("FoodContent 局部坐标中的出袋初速度，负 X、负 Y 表示朝左下方。")]
        [SerializeField] private Vector2 _pourVelocity = new Vector2(-120f, -60f);
        [SerializeField, Range(0f, 0.3f)] private float _pourSpeedVariation = 0.15f;
        [Tooltip("圆形碰撞半径占颗粒最长边的比例，调大时粮堆更蓬松。")]
        [SerializeField, Range(0.2f, 0.6f)] private float _collisionRadiusScale = 0.45f;

        private sealed class FoodParticle
        {
            public RectTransform Rect;
            public DogFoodPileSimulation.Body Body;
            public float Rotation;
            public float Spin;
            public bool Landed;
        }

        private readonly List<FoodParticle> _particles = new();
        private readonly List<Sprite> _availableSprites = new();
        private RectTransform _bagParent;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private Vector2 _grabOffset;
        private Camera _eventCamera;
        private Action _onCompleted;
        private DogFoodPourProgress _progress;
        private DogFoodPileSimulation _pile;
        private bool _initialized;
        private bool _running;
        private bool _dragging;
        private int _pointerId;

        public int LandedFoodCount => _progress?.Landed ?? 0;

        private bool Initialize()
        {
            if (_initialized) return true;
            if (_foodBag == null || _pourOrigin == null || _pourArea == null
                || _foodContent == null || !(_foodBag.parent is RectTransform parent)
                || !_pourOrigin.IsChildOf(_foodBag) || _foodContent.IsChildOf(_foodBag))
            {
                Debug.LogError("[PourDogFoodGame] 请绑定粮袋、袋口、接粮区域和 FoodContent；袋口必须在粮袋下，FoodContent 不能在粮袋下。", this);
                return false;
            }
            _availableSprites.Clear();
            if (_foodSprites != null)
                foreach (Sprite sprite in _foodSprites)
                    if (sprite != null) _availableSprites.Add(sprite);
            if (_availableSprites.Count == 0)
            {
                Debug.LogError("[PourDogFoodGame] 请在 Food Sprites 中绑定狗粮 Sprite。", this);
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
            _progress = new DogFoodPourProgress(Mathf.Max(1, _targetFoodCount));
            Rect bounds = _foodContent.rect;
            if (bounds.width <= 1f || bounds.height <= 1f)
                throw new InvalidOperationException("FoodContent 的宽高必须大于 1，请调整至碗的内部范围。");
            // 保留 Fall Duration 调节：按袋口到碗底的高度换算重力。
            float height = Mathf.Max(bounds.height, _foodContent.InverseTransformPoint(_pourOrigin.position).y - bounds.yMin);
            float duration = Mathf.Max(0.1f, _fallDuration);
            _pile = new DogFoodPileSimulation(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax,
                2f * height / (duration * duration));
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
            Sfx.Play(SoundId.DogFoodBag, this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_running || !_dragging || eventData.pointerId != _pointerId) return;
            if (!TryGetPointer(eventData, out Vector2 pointer)) return;
            Vector2 position = pointer + _grabOffset;
            if (((Vector2)_foodBag.localPosition - position).sqrMagnitude > 1f) Sfx.Play(SoundId.DogFoodBag, this);
            // 根节点可以只用于组织层级，不用它的默认 100×100 尺寸限制拖动。
            _foodBag.localPosition = new Vector3(position.x, position.y, _initialPosition.z);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragging && eventData.pointerId == _pointerId) ReleaseBag();
        }

        private bool TryGetPointer(PointerEventData eventData, out Vector2 pointer) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _bagParent, eventData.position, eventData.pressEventCamera, out pointer);

        private bool CanPour()
        {
            if (!_dragging) return false;
            // 每帧用倾倒姿态判断袋口，静止按住也会持续出粮。
            _foodBag.localRotation = _initialRotation * Quaternion.Euler(0f, 0f, _pourAngle);
            Vector2 mouthScreen = RectTransformUtility.WorldToScreenPoint(_eventCamera, _pourOrigin.position);
            if (RectTransformUtility.RectangleContainsScreenPoint(_pourArea, mouthScreen, _eventCamera))
                return true;
            _foodBag.localRotation = _initialRotation;
            return false;
        }

        private void Update()
        {
            if (!_running) return;
            float dt = Time.unscaledDeltaTime;
            // 已生成的颗粒继续落下；第 100 颗落地之前不生成第 101 颗。
            AdvanceParticles(dt);
            if (_progress.IsComplete)
            {
                Action completed = _onCompleted;
                End();
                completed?.Invoke();
                return;
            }
            bool canPour = CanPour() && _progress.Spawned < _progress.Target;
            if (_pourEffect != null) _pourEffect.SetActive(canPour);
            ActionSound.SetPlaying(SoundId.PourDogFood, canPour);
            int count = _progress.Tick(dt, canPour, Mathf.Max(1f, _foodsPerSecond));
            for (int i = 0; i < count; i++) SpawnFood();
        }

        private void SpawnFood()
        {
            Sprite sprite = _availableSprites[Random.Range(0, _availableSprites.Count)];
            var food = new GameObject("DogFood", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            food.layer = _foodContent.gameObject.layer;
            var rect = (RectTransform)food.transform;
            rect.SetParent(_foodContent, false);
            // 锚点对齐父物体 pivot，使 anchoredPosition 与父级局部坐标一致。
            rect.anchorMin = rect.anchorMax = _foodContent.pivot;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float size = Mathf.Max(1f, _foodSize) * Random.Range(1f - _sizeVariation, 1f + _sizeVariation);
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            rect.sizeDelta = aspect >= 1f ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
            var image = food.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Vector2 start = _foodContent.InverseTransformPoint(_pourOrigin.position);
            start.x += Random.Range(-_mouthSpread, _mouthSpread);
            Rect bounds = _foodContent.rect;
            float radius = Mathf.Min(size * _collisionRadiusScale, bounds.width * 0.49f);
            // 只赋予出袋初速度，不再根据碗的位置计算目标速度。
            Vector2 velocity = _pourVelocity * Random.Range(1f - _pourSpeedVariation, 1f + _pourSpeedVariation);
            float rotation = Random.Range(0f, 360f);
            rect.anchoredPosition = start;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _particles.Add(new FoodParticle
            {
                Rect = rect, Body = _pile.Add(start.x, start.y, radius, velocity.x, velocity.y),
                Rotation = rotation, Spin = Random.Range(-180f, 180f)
            });
        }

        private void AdvanceParticles(float dt)
        {
            _pile.Advance(dt);
            bool changed = false;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                FoodParticle particle = _particles[i];
                var body = particle.Body;
                if (body.Lost)
                {
                    if (!particle.Landed) _progress.DiscardInFlight();
                    particle.Rect.gameObject.SetActive(false);
                    Destroy(particle.Rect.gameObject);
                    _particles.RemoveAt(i);
                    continue;
                }
                particle.Rect.anchoredPosition = new Vector2(body.X, body.Y);
                if (!body.Landed) particle.Rotation += particle.Spin * dt;
                particle.Rect.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation);
                if (particle.Landed || !body.Landed) continue;
                particle.Landed = true;
                changed |= _progress.Land();
            }
            if (changed) UpdateProgress();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseBag();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) ReleaseBag();
        }
        private void OnDisable()
        {
            End();
            ClearParticles();
        }

        private void ReleaseBag()
        {
            _dragging = false;
            _progress?.Tick(0f, false, _foodsPerSecond);
            if (_pourEffect != null) _pourEffect.SetActive(false);
            if (!_initialized) return;
            _foodBag.localPosition = _initialPosition;
            _foodBag.localRotation = _initialRotation;
        }

        public override void End()
        {
            ActionSound.StopAll();
            _running = false;
            _onCompleted = null;
            ReleaseBag();
        }

        public override void ResetView()
        {
            End();
            ClearParticles();
            _progress = null;
            UpdateProgress();
        }

        private void ClearParticles()
        {
            // 只清理由本组件创建的颗粒，不影响 FoodContent 中的其他物体。
            foreach (FoodParticle particle in _particles)
                if (particle.Rect != null)
                {
                    particle.Rect.gameObject.SetActive(false);
                    Destroy(particle.Rect.gameObject);
                }
            _particles.Clear();
            _pile = null;
        }

        private void UpdateProgress()
        {
            float progress = _progress?.Progress ?? 0f;
            if (_foodFill != null) _foodFill.fillAmount = progress;
            if (_progressBar != null) _progressBar.normalizedValue = progress;
        }
    }
}
