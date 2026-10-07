using TMPro;
using TapTapGameJam.MiniGame;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>一个 Arrow 只绑定一个任务；根物体平移，只有 ArrowImage 旋转。</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class Arrow : MonoBehaviour
    {
        [SerializeField] private TMP_Text _taskName;
        [SerializeField] private TMP_Text _taskReward;
        [SerializeField] private RectTransform _arrowImage;
        [Tooltip("图片默认朝上时为 -90，朝右时为 0。")]
        [SerializeField] private float _angleOffset = -90f;
        private MiniGameInstanceBasic _instance;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Canvas _canvas;
        private bool _visible;
        private static Sprite _defaultArrowSprite;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
            if (_taskName == null)
                _taskName = transform.Find("TaskName")?.GetComponent<TMP_Text>();
            if (_taskReward == null)
                _taskReward = transform.Find("TaskReward")?.GetComponent<TMP_Text>();
            if (_arrowImage == null)
                _arrowImage = transform.Find("ArrowImage") as RectTransform;

            // 隐藏只改变透明度，组件继续参与位置更新；指引不拦截鼠标。
            _group.interactable = false;
            _group.blocksRaycasts = false;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
                
            EnsureArrowSprite();
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
            if (_taskName != null)
                _taskName.text = string.IsNullOrEmpty(instance.GuideText)
                    ? "前往任务地点" : instance.GuideText;
            if (_taskReward != null)
                _taskReward.text = instance.RewardTalentPoints > 0
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

            float halfWidth = area.rect.width * 0.5f - _rect.rect.width * 0.5f - padding;
            float halfHeight = area.rect.height * 0.5f - _rect.rect.height * 0.5f - padding;
            if (!TaskIndicatorGeometry.TryGetEdgePosition(direction.x, direction.y,
                halfWidth, halfHeight, out float x, out float y))
            {
                SetVisible(false);
                return;
            }

            _rect.anchoredPosition = new Vector2(x, y);
            if (_arrowImage != null)
                _arrowImage.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + _angleOffset);
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

        /// <summary>
        /// 没有提供箭头Image时自己画一个
        /// </summary>
        private void EnsureArrowSprite()
        {
            Image image = _arrowImage != null ? _arrowImage.GetComponent<Image>() : null;
            if (image == null || image.sprite != null) return;
            // 预制体尚未指定 Sprite 时，提供一个默认向上的箭头。
            if (_defaultArrowSprite == null)
            {
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "TaskIndicatorArrow";
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool head = y >= 16 && Mathf.Abs(x - 15.5f) <= (31 - y) + 0.5f;
                        bool shaft = y >= 2 && y <= 17 && x >= 11 && x <= 20;
                        pixels[y * size + x] = head || shaft ? Color.white : Color.clear;
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                _defaultArrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), size);
            }
            image.sprite = _defaultArrowSprite;
            image.preserveAspect = true;
        }

    }

}
