using System.Collections.Generic;
using TapTapGameJam.Audio;
using TapTapGameJam.Cockroach;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 巢穴的**命令环**：一个盖在巢穴上方的圆形 World Space 按钮，取代原来"点巢穴本体"的
    /// <see cref="Button3D"/>。点一下就切换虫群指令（归巢 / 觅食）。
    ///
    /// 【为什么整块 UI 都在运行时搭】
    /// 圆形多层 + 环形波纹 + 高频改色，全都要靠代码驱动；如果做成预制体里几十个 Image，
    /// 调一个参数（比如直径、层厚、颜色）就得挨个改。所以这里只保留"一个组件"，
    /// 层次、Sprite、动画都在 <see cref="BuildHierarchy"/> 里生成，
    /// 预制体里能看到的只有本组件和几个可调参数。
    ///
    /// 【显示 / 颜色和范围圈同步】
    /// 显示条件直接读 <see cref="CockNest.IsRangeRingVisible"/>（范围圈用的就是同一条判断），
    /// 颜色读 <see cref="CockNest.HatchColor"/> / <see cref="CockNest.ForagingColor"/>。
    /// 两边共用一个来源，不会出现"圈出来了按钮没出来"这种事。
    ///
    /// 【点击判定】
    /// 不用 Collider、不用 GraphicRaycaster，而是把按钮中心投影到屏幕、按世界半径换算成像素半径
    /// 再做圆形距离判定。好处：
    /// - 判定范围和看到的圆完全一致（Collider 是方的，边角会误触）；
    /// - 不会挡玩家脚下的交互射线（巢穴本体还挂着 Interactable 的触发器）。
    ///
    /// 【视觉层次（由外到内）】
    /// 光晕 → 刻度环（缓慢自转）→ 外描边 → 底盘（压暗，保证字可读）→ 内描边 → 涟漪 / 点击闪光 → 文字。
    /// </summary>
    [DisallowMultipleComponent]
    public class NestCommandButton : MonoBehaviour
    {
        #region 配置

        [Header("绑定")]
        [Tooltip("要指挥的巢穴。留空则往上找 CockNest")]
        [SerializeField] private CockNest _nest;

        [Tooltip("文字字体。留空用 TMP 默认字体（默认字体没有中文字形，会变方块，所以预制体里连了阿里普惠体）")]
        [SerializeField] private TMP_FontAsset _font;

        [Header("摆放")]
        [Tooltip("按钮直径，世界单位。巢穴本体半径约 0.6，太大就会盖住整个巢穴")]
        [SerializeField, Min(0.05f)] private float _worldDiameter = 0.95f;

        [Tooltip("离巢穴原点的高度，世界单位")]
        [SerializeField] private float _height = 1.15f;

        [Tooltip("渲染层。要和范围圈同一层（默认 WorldUI），才会被 UI 相机盖在 3D 之上")]
        [SerializeField] private string _layerName = "WorldUI";

        [Tooltip("Canvas 排序值。比巢穴自己的 UI 大一点，保证压在上面")]
        [SerializeField] private int _sortingOrder = 20;

        [Header("交互")]
        [Tooltip("关掉就只显示不响应鼠标")]
        [SerializeField] private bool _interactable = true;

        [Tooltip("点击判定半径的宽容系数。1.0 = 严格按画布边缘，1.15 = 往外多给 15%")]
        [SerializeField, Min(1f)] private float _hitPadding = 1.15f;

        [Tooltip("判定半径的屏幕像素下限。相机拉远时按钮在屏幕上变小，这个值保证它始终好点")]
        [SerializeField, Min(0f)] private float _minScreenRadius = 40f;

        #endregion

        #region 设计常量（想改视觉就改这里）

        /// <summary>设计基准尺寸。所有层的像素值都按这个尺寸算，最后整体缩放到世界直径</summary>
        private const float DesignSize = 256f;

        private const float GlowSize = 330f;      // 光晕（故意大于画布，做出弥散感）
        private const float TickRingSize = 252f;  // 外圈刻度
        private const float OuterRimSize = 234f;  // 外描边
        private const float BackPlateSize = 212f; // 底盘
        private const float InnerRimSize = 166f;  // 内描边
        private const float RippleSize = 196f;    // 悬停涟漪的起始直径
        private const float FlashSize = 208f;     // 点击闪光的起始直径
        private const float LabelSize = 224f;
        private const float LabelFontSize = 76f;
        private const int TickCount = 24;

        /// <summary>一圈涟漪的时长（秒）。三条错开 1/3 相位循环</summary>
        private const float RipplePeriod = 1.15f;
        private const int RippleCount = 3;

        /// <summary>点击闪光时长（秒）</summary>
        private const float FlashDuration = 0.32f;

        private static readonly Color BackPlateColor = new Color(0.05f, 0.06f, 0.09f, 0.68f);
        private static readonly Color DisabledTint = new Color(0.55f, 0.58f, 0.62f, 1f);

        #endregion

        #region 运行时对象

        private GameObject _canvasObject;
        private RectTransform _canvasRect;
        private RectTransform _rootRect;

        private Image _glow;
        private Image _tickRing;
        private Image _outerRim;
        private Image _backPlate;
        private Image _innerRim;
        private readonly Image[] _ripples = new Image[RippleCount];
        private Image _flash;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _labelShadow;

        private Camera _camera;
        private int _layer = -1;

        #endregion

        #region 动画状态

        private CockroachCommand _command = CockroachCommand.Hatch;
        private Color _commandColor = CockNest.HatchColor;
        private float _hover;          // 0..1，鼠标悬停的平滑值
        private float _ripplePhase;    // 0..1 循环
        private float _flashTime = 1f; // >=1 表示闪光播完了
        private float _labelPunch;     // 0..1，换字时的一下弹跳
        private float _pressPunch;     // 0..1，按下时的下压
        private float _tickAngle;
        private bool _hovered;
        private bool _pointerDown;
        // UI 射线复用的暂存对象：每帧 new 一个 List<RaycastResult> 会一直产生 GC
        private static PointerEventData _uiRaycastData;
        private static readonly List<RaycastResult> _uiRaycastHits = new List<RaycastResult>();

        private static Sprite _discSprite;
        private static Sprite _glowSprite;
        private static Sprite _thinRingSprite;
        private static Sprite _mediumRingSprite;
        private static Sprite _tickRingSprite;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (_nest == null) _nest = GetComponentInParent<CockNest>();
            if (_font == null)
            {
                Debug.LogWarning(
                    $"[NestCommandButton] {name} 没连中文字体，会用 TMP 默认字体（没有中文字形，按钮上的字会变成方块）。" +
                    "请在 Inspector 里把 _font 连到 AlibabaPuHuiTi SDF。", this);
            }

            _layer = LayerMask.NameToLayer(_layerName);
            if (_layer < 0) _layer = gameObject.layer;

            BuildHierarchy();
            SetShown(false);
        }

        private void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
        }

        /// <summary>Play 模式下选中本物体时画出实际的点击判定范围，方便确认"点不到"是不是判定圈的问题</summary>
        private void OnDrawGizmosSelected()
        {
            if (_canvasRect == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.7f, 0.9f);
            Gizmos.DrawWireSphere(_canvasRect.position, WorldRadius * _hitPadding);
        }

        private void Update()
        {
            if (_nest == null || _canvasObject == null) return;

            // 和范围圈共用同一条显示判断
            bool shown = _nest.IsRangeRingVisible;
            SetShown(shown);
            if (!shown)
            {
                _hovered = false;
                _pointerDown = false;
                return;
            }

            float dt = Time.unscaledDeltaTime;

            RefreshCommand(dt);
            RefreshPointer();
            RefreshVisuals(dt);
        }

        #endregion

        #region 搭建

        private void BuildHierarchy()
        {
            // ---- Canvas（世界空间）----
            _canvasObject = new GameObject("NestCommandButtonCanvas", typeof(RectTransform));
            _canvasObject.layer = _layer;
            _canvasObject.transform.SetParent(transform, false);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = _sortingOrder;

            // 刻意不加 GraphicRaycaster：本按钮走自己的圆形屏幕判定，
            // 加了反而会让 EventSystem 认为"鼠标在 UI 上"，把世界里的交互挡掉。
            _canvasObject.AddComponent<WorldBillboardUI>();

            _canvasRect = (RectTransform)_canvasObject.transform;
            _canvasRect.sizeDelta = new Vector2(DesignSize, DesignSize);
            _canvasRect.localPosition = new Vector3(0f, _height, 0f);
            _canvasRect.localRotation = Quaternion.identity;
            _canvasRect.localScale = Vector3.one * (_worldDiameter / DesignSize);

            // ---- 动画根：缩放动画都作用在它身上，不碰 Canvas 的世界缩放 ----
            GameObject rootObject = CreateRect("Root", _canvasRect, DesignSize);
            rootObject.layer = _layer;
            _rootRect = (RectTransform)rootObject.transform;

            // ---- 各层（由外到内，后面的盖在前面的上面）----
            _glow = CreateImage("Glow", _rootRect, GlowSize, GlowSprite());
            _tickRing = CreateImage("TickRing", _rootRect, TickRingSize, TickRingSprite());
            _outerRim = CreateImage("OuterRim", _rootRect, OuterRimSize, MediumRingSprite());
            _backPlate = CreateImage("BackPlate", _rootRect, BackPlateSize, DiscSprite());
            _backPlate.color = BackPlateColor;
            _innerRim = CreateImage("InnerRim", _rootRect, InnerRimSize, ThinRingSprite());

            for (int i = 0; i < RippleCount; i++)
            {
                _ripples[i] = CreateImage($"Ripple{i}", _rootRect, RippleSize, ThinRingSprite());
            }

            _flash = CreateImage("ClickFlash", _rootRect, FlashSize, MediumRingSprite());

            // ---- 文字：阴影 + 主字，双层保证在浅色地面上也读得清 ----
            _labelShadow = CreateLabel("LabelShadow", _rootRect, new Color(0f, 0f, 0f, 0.55f));
            _labelShadow.rectTransform.anchoredPosition = new Vector2(0f, -5f);

            _label = CreateLabel("Label", _rootRect, Color.white);
            SetCommandText(_command);
        }

        private GameObject CreateRect(string name, Transform parent, float size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = _layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(size, size);
            return go;
        }

        private Image CreateImage(string name, Transform parent, float size, Sprite sprite)
        {
            Image image = CreateRect(name, parent, size).AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;   // 不参与 UI 射线，避免挡住世界里的交互
            image.color = Color.white;
            return image;
        }

        private TextMeshProUGUI CreateLabel(string name, Transform parent, Color color)
        {
            GameObject go = CreateRect(name, parent, LabelSize);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

            TMP_FontAsset font = _font != null ? _font : TMP_Settings.defaultFontAsset;
            if (font != null) text.font = font;

            text.text = "归巢";
            text.fontSize = LabelFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.characterSpacing = 6f;
            text.color = color;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = Vector2.zero;
            return text;
        }

        #endregion

        #region 状态同步

        /// <summary>显示 / 隐藏。和范围圈同一帧切换</summary>
        private void SetShown(bool shown)
        {
            if (_canvasObject == null || _canvasObject.activeSelf == shown) return;

            _canvasObject.SetActive(shown);
            if (!shown)
            {
                // 收起时把瞬时状态清掉，下次出来不会从"半边涟漪"开始
                _hover = 0f;
                _flashTime = 1f;
                _pressPunch = 0f;
            }
        }

        /// <summary>指令变了就换文案、换色、换刻度环转向，并给文字一下弹跳</summary>
        private void RefreshCommand(float dt)
        {
            CockroachCommand command = _nest.CurrentCommand;
            if (command != _command)
            {
                _command = command;
                SetCommandText(command);
                _labelPunch = 1f;
            }

            Color target = command == CockroachCommand.Hatch ? CockNest.HatchColor : CockNest.ForagingColor;
            float t = 1f - Mathf.Exp(-14f * dt);   // 帧率无关的指数逼近
            _commandColor = Color.Lerp(_commandColor, target, t);
        }

        private void SetCommandText(CockroachCommand command)
        {
            string text = command == CockroachCommand.Hatch ? "觅食" : "归巢";
            if (_label != null) _label.text = text;
            if (_labelShadow != null) _labelShadow.text = text;
        }

        /// <summary>巢穴已解锁（虫群真的在听指令）时按钮才可点，否则整块压暗</summary>
        private bool IsActionable => _interactable && _nest != null && _nest.isCockroachActive;

        #endregion

        #region 鼠标

        private void RefreshPointer()
        {
            if (!IsActionable || IntroVideoTransition.IsTransitioning || Time.timeScale <= 0f)
            {
                _hovered = false;
                _pointerDown = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _hovered = false;
                _pointerDown = false;
                return;
            }

            Vector2 screenPoint = mouse.position.ReadValue();

            // 只让**屏幕空间**的 UI（背包 / HUD）把点击吃掉。
            // 不能用 EventSystem.IsPointerOverGameObject()：巢穴自己的气泡、地面环都是
            // World Space Canvas，它们的 Graphic 会把整片区域判成"指针在 UI 上"，
            // 结果按钮只有没被任何世界 UI 盖住的那一小块（看起来就是"只有中心能摸到"）。
            bool over = IsPointerOnButton(screenPoint) && !IsPointerOverScreenSpaceUI(screenPoint);

            if (over != _hovered)
            {
                _hovered = over;
                if (_hovered) Sfx.Play(SoundId.Switch, this);
            }

            bool down = mouse.leftButton.isPressed;

            // 只在"按下的那一帧正好指着按钮"时触发：从外面按着拖进来不算，按住不放也不会连点
            if (over && down && !_pointerDown)
            {
                TriggerClick();
            }

            _pointerDown = down;
        }

        private void TriggerClick()
        {
            _flashTime = 0f;
            _pressPunch = 1f;
            _nest.CycleCommand();
            Sfx.Play(SoundId.Click, this);
        }

        /// <summary>
        /// 按钮当前的**世界半径**。读的是画布真实的 lossyScale，而不是 <see cref="_worldDiameter"/>：
        /// 巢穴（或它的父节点）被缩放过时，画布会跟着变大，但配置值不会 ——
        /// 用配置值判定就会出现"看着很大、实际只有中心能点到"。
        /// </summary>
        private float WorldRadius
        {
            get
            {
                if (_canvasRect == null) return _worldDiameter * 0.5f;

                Vector3 scale = _canvasRect.lossyScale;
                float uniform = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                return _canvasRect.rect.width * 0.5f * uniform;
            }
        }

        /// <summary>
        /// 屏幕点是否落在圆内。把按钮中心和一个"中心 + 世界半径"的点都投到屏幕上，
        /// 两者像素距离就是按钮在屏幕上的半径（透视相机下这样换算才不会随距离失真）。
        /// 另外给一个像素下限，相机拉远时也不会变得难以点中。
        /// </summary>
        private bool IsPointerOnButton(Vector2 screenPoint)
        {
            Camera cam = CameraForPointer();
            if (cam == null) return false;

            Vector3 center = _canvasRect.position;
            Vector3 centerScreen = cam.WorldToScreenPoint(center);
            if (centerScreen.z <= 0f) return false;   // 在相机背后

            Vector3 edge = center + cam.transform.right * WorldRadius;
            Vector3 edgeScreen = cam.WorldToScreenPoint(edge);
            if (edgeScreen.z <= 0f) return false;

            float radius = Vector2.Distance(centerScreen, edgeScreen) * _hitPadding;
            radius = Mathf.Max(radius, _minScreenRadius);

            return Vector2.Distance(screenPoint, centerScreen) <= radius;
        }

        private Camera CameraForPointer()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        /// <summary>
        /// 指针是否落在**屏幕空间**的 UI 上（背包、HUD 这类）。
        ///
        /// 特意不用 <c>EventSystem.IsPointerOverGameObject()</c>：它把 World Space Canvas 也算进去，
        /// 而巢穴旁边就有气泡 / 地面环两个世界空间 Canvas。它们的大矩形会盖住按钮的屏幕区域，
        /// 于是按钮只有没被盖住的那一小块能触发 —— 表现就是"只有中间能点到"。
        ///
        /// 这里自己射线一遍，只看 GraphicRaycaster 挂在 ScreenSpaceOverlay / ScreenSpaceCamera 上的结果。
        /// </summary>
        private static bool IsPointerOverScreenSpaceUI(Vector2 screenPoint)
        {
            EventSystem eventSystem = EventSystem.current;
            Mouse mouse = Mouse.current;
            if (eventSystem == null || mouse == null) return false;

            _uiRaycastData ??= new PointerEventData(eventSystem);
            _uiRaycastData.position = screenPoint;
            _uiRaycastHits.Clear();
            eventSystem.RaycastAll(_uiRaycastData, _uiRaycastHits);

            for (int i = 0; i < _uiRaycastHits.Count; i++)
            {
                if (_uiRaycastHits[i].module is not GraphicRaycaster raycaster) continue;

                Canvas canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null) continue;

                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                    canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 动画

        private void RefreshVisuals(float dt)
        {
            bool actionable = IsActionable;

            // ---- 悬停程度 ----
            _hover = Mathf.MoveTowards(_hover, _hovered && actionable ? 1f : 0f, dt * 7f);

            // ---- 按下回弹 ----
            _pressPunch = Mathf.MoveTowards(_pressPunch, 0f, dt * 4.5f);
            _flashTime = Mathf.Min(1f, _flashTime + dt / FlashDuration);
            _labelPunch = Mathf.MoveTowards(_labelPunch, 0f, dt * 4f);

            // ---- 整体缩放：悬停微微放大，按下先压一下 ----
            float scale = 1f + 0.045f * _hover - 0.07f * Mathf.Sin(_pressPunch * Mathf.PI);
            _rootRect.localScale = Vector3.one * scale;

            // ---- 命令色（不可点时转灰）----
            Color color = actionable ? _commandColor : Color.Lerp(_commandColor, DisabledTint, 0.75f);
            float dim = actionable ? 1f : 0.55f;

            // ---- 呼吸：光晕和刻度缓慢起伏，让静止状态也活着 ----
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.7f);

            SetAlpha(_glow, color, (0.10f + 0.05f * breath + 0.16f * _hover) * dim);
            SetAlpha(_tickRing, color, (0.26f + 0.20f * _hover) * dim);
            SetAlpha(_outerRim, color, (0.86f + 0.14f * _hover) * dim);
            SetAlpha(_innerRim, color, (0.30f + 0.22f * _hover) * dim);
            _backPlate.color = Color.Lerp(BackPlateColor,
                new Color(BackPlateColor.r + 0.06f, BackPlateColor.g + 0.07f, BackPlateColor.b + 0.09f,
                    BackPlateColor.a + 0.08f), _hover);

            // ---- 刻度环自转：归巢顺时针、觅食逆时针（和范围圈的转向语义一致）----
            float spin = _command == CockroachCommand.Hatch ? 10f : -20f;
            _tickAngle += dt * spin * (1f + 0.8f * _hover);
            if (_tickAngle > 360f) _tickAngle -= 360f;
            if (_tickAngle < -360f) _tickAngle += 360f;
            _tickRing.rectTransform.localEulerAngles = new Vector3(0f, 0f, _tickAngle);

            // ---- 悬停涟漪：三条错相位的圆环，从内圈向外扩散淡出 ----
            _ripplePhase += dt / RipplePeriod;
            if (_ripplePhase >= 1f) _ripplePhase -= 1f;

            for (int i = 0; i < RippleCount; i++)
            {
                Image ripple = _ripples[i];
                float phase = Mathf.Repeat(_ripplePhase + i / (float)RippleCount, 1f);
                float grow = Mathf.Lerp(0.62f, 1.06f, phase);
                ripple.rectTransform.sizeDelta = new Vector2(RippleSize * grow, RippleSize * grow);

                float alpha = (1f - phase) * (1f - phase) * 0.75f * _hover;
                SetAlpha(ripple, color, alpha * dim);
            }

            // ---- 点击闪光：一圈快速扩散的白环 + 文字闪白 ----
            if (_flashTime < 1f)
            {
                float p = _flashTime;
                float ease = 1f - (1f - p) * (1f - p);
                float grow = Mathf.Lerp(0.55f, 1.32f, ease);
                _flash.rectTransform.sizeDelta = new Vector2(FlashSize * grow, FlashSize * grow);
                SetAlpha(_flash, Color.Lerp(Color.white, color, 0.35f), (1f - p) * 0.85f);
            }
            else
            {
                SetAlpha(_flash, color, 0f);
            }

            // ---- 文字：换字时弹一下，点击时闪白 ----
            float labelScale = 1f + 0.18f * _labelPunch;
            _label.rectTransform.localScale = Vector3.one * labelScale;
            _labelShadow.rectTransform.localScale = Vector3.one * labelScale;

            // 平时文字带一点命令色的暖调，点下去瞬间冲到纯白，读起来像"被点亮"了一下
            Color labelBase = Color.Lerp(Color.white, color, 0.22f);
            float flashWhite = (1f - _flashTime) * (1f - _flashTime);
            _label.color = Color.Lerp(labelBase, Color.white, flashWhite);
            _labelShadow.color = new Color(0f, 0f, 0f, 0.55f + 0.25f * flashWhite);
        }

        private static void SetAlpha(Image image, Color rgb, float alpha)
        {
            image.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha));
        }

        #endregion

        #region 程序化贴图

        // 贴图是静态共享的：同屏多个巢穴只生成一份。
        // 域重载后 Unity 会把它们判成 fake-null，所以这里用 null 判断顺带重建。
        private static Sprite DiscSprite() => _discSprite != null ? _discSprite : _discSprite = BuildDisc(128);
        private static Sprite GlowSprite() => _glowSprite != null ? _glowSprite : _glowSprite = BuildGlow(128);
        private static Sprite ThinRingSprite() => _thinRingSprite != null ? _thinRingSprite : _thinRingSprite = BuildRing(256, 0.86f, 0.05f);
        private static Sprite MediumRingSprite() => _mediumRingSprite != null ? _mediumRingSprite : _mediumRingSprite = BuildRing(256, 0.90f, 0.10f);
        private static Sprite TickRingSprite() => _tickRingSprite != null ? _tickRingSprite : _tickRingSprite = BuildTickRing(256, TickCount);

        private static Sprite BuildDisc(int size)
        {
            return BuildSprite(size, "NestButtonDisc", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return 1f - Smooth(1f - Edge(size), 1f, r);
            });
        }

        private static Sprite BuildGlow(int size)
        {
            return BuildSprite(size, "NestButtonGlow", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float t = Mathf.Clamp01(1f - r);
                return t * t * t;   // 三次衰减：中心亮、边缘几乎全透明
            });
        }

        private static Sprite BuildRing(int size, float radius, float thickness)
        {
            float inner = radius - thickness;
            float outer = radius;
            return BuildSprite(size, "NestButtonRing", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Smooth(inner - Edge(size), inner, r);          // 内边
                a *= 1f - Smooth(outer, outer + Edge(size), r);          // 外边
                return a;
            });
        }

        /// <summary>带缺口的刻度环：一圈等分的短虚线，用来做"仪表盘"质感的自转层</summary>
        private static Sprite BuildTickRing(int size, int ticks)
        {
            const float radius = 0.94f;
            const float thickness = 0.05f;
            const float duty = 0.42f;   // 每格实线占比

            return BuildSprite(size, "NestButtonTickRing", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float band = Smooth(radius - thickness - Edge(size), radius - thickness, r);
                band *= 1f - Smooth(radius, radius + Edge(size), r);
                if (band <= 0f) return 0f;

                float angle = Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 0.5f;   // 0..1
                float segment = Mathf.Repeat(angle * ticks, 1f);
                float feather = 0.06f;
                float dash = Smooth(0f, feather, segment) * (1f - Smooth(duty - feather, duty, segment));
                return band * dash;
            });
        }

        private static float Edge(int size) => 1.5f / Mathf.Max(1, size);

        private static float Smooth(float from, float to, float x)
        {
            if (Mathf.Approximately(from, to)) return x < from ? 0f : 1f;
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>逐像素生成一张白色 + alpha 的贴图，颜色交给 Image.color 去乘</summary>
        private static Sprite BuildSprite(int size, string name, System.Func<float, float, float> alphaAt)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                // 像素中心映射到 [-1, 1]；v 从下往上
                float v = ((y + 0.5f) / size) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float u = ((x + 0.5f) / size) * 2f - 1f;
                    float a = Mathf.Clamp01(alphaAt(u, v));
                    Color32 c = white;
                    c.a = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = a > 0f ? c : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);   // 保留 CPU 副本：Sprite.Create 用 FullRect 虽然不读像素，但留着最稳（128² 才几十 KB）

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
        }

        #endregion
    }
}
