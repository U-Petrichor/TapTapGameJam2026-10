using TMPro;
using TapTapGameJam.MiniGame;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>内容保持正向，箭头根据任务方向绕内容中心移动并旋转。</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class Arrow : MonoBehaviour
    {
        [SerializeField] private Image _taskIcon;
        [SerializeField] private Sprite _deliveryIcon;
        [SerializeField] private Sprite _sortFileIcon;
        [SerializeField] private Sprite _coffeeIcon;
        [SerializeField] private Sprite _dogFoodIcon;
        [SerializeField] private Sprite _printerIcon;
        [SerializeField] private Sprite _cleanIcon;
        [SerializeField] private Sprite _parcelIcon;
        [SerializeField] private Sprite _stockIcon;
        [SerializeField] private TMP_Text _taskReward;
        [SerializeField] private RectTransform _arrowImage;
        [Tooltip("圆周中心与半径参考点，可在预制体中调整。")]
        [SerializeField] private RectTransform _orbitCenter;
        [SerializeField] private RectTransform _orbitRadiusPoint;
        [SerializeField, Min(0f)] private float _directionSmoothTime = 0.08f;
        [Tooltip("图片默认朝上时为 -90，朝右时为 0。")]
        [SerializeField] private float _angleOffset = -90f;
        private MiniGameInstanceBasic _instance;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Canvas _canvas;
        private bool _visible;
        private float _displayAngle;
        private float _angleVelocity;
        private RectTransform[] _visualRects;
        private readonly Vector3[] _corners = new Vector3[4];

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
            if (_taskReward == null)
                _taskReward = transform.Find("TaskReward")?.GetComponent<TMP_Text>();
            if (_arrowImage == null)
                _arrowImage = transform.Find("ArrowImage") as RectTransform;

            // 隐藏只改变透明度，组件继续参与位置更新；指引不拦截鼠标。
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var graphics = GetComponentsInChildren<Graphic>(true);
            _visualRects = new RectTransform[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
                _visualRects[i] = graphics[i].rectTransform;
            }
            SetVisible(false);
        }

        public void Bind(MiniGameInstanceBasic instance)
        {
            Unbind();
            _instance = instance;
            if (_instance == null) return;
            _instance.EventTargetChanged += Refresh;
            _instance.EventStageChanged += Refresh;

            Refresh(instance);
        }
        public void Unbind()
        {
            if (_instance != null)
            {
                _instance.EventTargetChanged -= Refresh;
                _instance.EventStageChanged -= Refresh;
            }
            _instance = null;
            if (_group != null) SetVisible(false);
        }
        private void Refresh(MiniGameInstanceBasic instance)
        {
            if (_taskIcon != null)
                _taskIcon.sprite = instance switch
                {
                    SortFileInstance _ => _sortFileIcon,
                    MakeCoffeeInstance _ => _coffeeIcon,
                    PourDogFoodInstance _ => _dogFoodIcon,
                    PrinterRepairInstance _ => _printerIcon,
                    CleanInstance _ => _cleanIcon,
                    ParcelCutInstance _ => _parcelIcon,
                    StockInstance _ => _stockIcon,
                    _ => _deliveryIcon // 送文件。
                };
            if (_taskReward != null)
                _taskReward.text = instance.IsDailyActivity ? instance.AvailabilityText.Replace(" · ", "\n") : instance.RewardTalentPoints > 0
                    ? $"{instance.RewardCoins} 金币 · {instance.RewardTalentPoints} 天赋点"
                    : $"{instance.RewardCoins} 金币";
        }

        // 由 TaskIndicatorUI 在镜头更新后调用。
        public void UpdatePosition(RectTransform area, Camera camera, float padding)
        {
            if (_instance == null || _instance.IsFinished ||
                _instance.TargetPosition == null || camera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 world = _instance.TargetPosition.position;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            // 箭头已经显示时，目标进入画面内侧一点才隐藏，减少边界闪烁。
            float inset = _visible ? 0.015f : 0f;
            if (viewport.z > 0f && viewport.x >= inset && viewport.x <= 1f - inset &&
                viewport.y >= inset && viewport.y <= 1f - inset)
            {
                SetVisible(false);
                return;
            }

            Vector2 direction;
            if (viewport.z > 0f)
            {
                Camera uiCamera = _canvas != null &&
                    _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area,
                    camera.WorldToScreenPoint(world), uiCamera, out Vector2 projected))
                {
                    SetVisible(false);
                    return;
                }

                direction = projected - area.rect.center;
            }
            else
            {
                // 相机背后的透视投影会翻转方向，改用相机局部坐标。
                Vector3 local = camera.transform.InverseTransformPoint(world);
                direction = new Vector2(local.x * camera.projectionMatrix.m00 * area.rect.width,
                    local.y * camera.projectionMatrix.m11 * area.rect.height);
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;

            UpdateOrbit(direction);
            // 使用实际图案、奖励和旋转后箭头的范围，避免靠边时被裁切。
            Vector2 extents = GetVisualExtents(area);
            float halfWidth = area.rect.width * 0.5f - extents.x - padding;
            float halfHeight = area.rect.height * 0.5f - extents.y - padding;
            if (!TaskIndicatorGeometry.TryGetEdgePosition(direction.x, direction.y,
                halfWidth, halfHeight, out float x, out float y))
            {
                SetVisible(false);
                return;
            }

            Vector2 position = area.rect.center + new Vector2(x, y);
            _rect.localPosition = new Vector3(position.x, position.y, _rect.localPosition.z);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _group.alpha = visible ? 1f : 0f;
        }

       
        private void OnDestroy()
        {
            Unbind();
        }

        private void UpdateOrbit(Vector2 direction)
        {
            if (_arrowImage == null || _orbitCenter == null || _orbitRadiusPoint == null) return;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (!_visible || _directionSmoothTime <= 0f)
            {
                _displayAngle = targetAngle;
                _angleVelocity = 0f;
            }
            else
                _displayAngle = Mathf.SmoothDampAngle(_displayAngle, targetAngle, ref _angleVelocity,
                    _directionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 center = _rect.InverseTransformPoint(_orbitCenter.position);
            Vector3 radiusPoint = _rect.InverseTransformPoint(_orbitRadiusPoint.position);
            float radius = Vector2.Distance(center, radiusPoint);
            float radians = _displayAngle * Mathf.Deg2Rad;
            _arrowImage.localPosition = center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            _arrowImage.localRotation = Quaternion.Euler(0f, 0f, _displayAngle + _angleOffset);
        }

        private Vector2 GetVisualExtents(RectTransform area)
        {
            Vector2 extents = Vector2.zero;
            Vector3 origin = area.InverseTransformPoint(_rect.position);
            foreach (var visual in _visualRects)
            {
                if (!visual.gameObject.activeInHierarchy) continue;
                visual.GetWorldCorners(_corners);
                foreach (var corner in _corners)
                {
                    Vector3 relative = area.InverseTransformPoint(corner) - origin;
                    extents.x = Mathf.Max(extents.x, Mathf.Abs(relative.x));
                    extents.y = Mathf.Max(extents.y, Mathf.Abs(relative.y));
                }
            }
            return extents;
        }

    }

}
