using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.Core
{
    /// <summary>
    /// 一天的光照。挂在方向光（Directional Light）上，根据 <see cref="GamePhaseManager"/> 的阶段推进
    /// 调整 X 轴角度、颜色和强度。
    ///
    /// ============================ 角度 ============================
    /// <b>只改 X</b>：Y / Z 在 Awake 里从 Transform 上读一次记下来，之后一直不动。
    /// 在 Y/Z 固定的前提下，方向光的朝向是
    ///     forward = (cos x · sin y, −sin x, cos x · cos y)
    /// 也就是方位角只由 y 决定，x 负责"抬高 / 压低太阳"：
    ///     x = 0    光几乎水平（贴着地平线，影子最长）
    ///     x = 90   光垂直向下（正午，影子最短）
    ///     x = 180  又回到水平
    ///
    /// 【为什么只动 X 也能做出"东升西落"】
    /// x 跨过 90° 时 cos x 变号，水平分量整条翻到对面：
    ///     x = 25°   forward 水平分量 = +0.906·(sin y, cos y)   → 从"东"边打过来
    ///     x = 155°  forward 水平分量 = −0.906·(sin y, cos y)   → 从"西"边打过来
    /// 两者高度角完全一样（sin25° = sin155°）。而 x ∈ (0°, 180°) 时 −sin x 恒为负，
    /// 光永远是从上方打下来的，不会从地底照上来。
    /// 所以：<b>早 / 晚两个角度必须分列 90° 两侧</b>，才会出现方位翻转；同侧的话就只是高度角伸缩。
    ///
    /// 注意：X 现在完全由脚本接管，Transform 上原来那个 X 不再起作用；要配的是 Y / Z。
    ///
    /// ============================ 时间轴 ============================
    /// 注意 DayProcess / NightProcess 是<b>剩余比例</b>（刚进入阶段 = 1，阶段结束 = 0），
    /// 所以推进进度要反一下：t = 1 − 剩余比例。t 的 0 / 0.5 / 1 对应早 / 中 / 晚：
    ///     白天：早晨 → 正午 → 黄昏
    ///     夜晚：同样按早 → 中 → 晚推进（月升 → 月落）
    ///
    ///   角度：早 → 中 → 晚三段用 smoothstep 插值。正午处斜率为 0，
    ///         所以光会在正午附近"停"一下再翻向，不是匀速扫过去。
    ///   颜色 / 强度：t ∈ [0, 0.5] 早晨 → 正午，t ∈ [0.5, 1] 正午 → 黄昏，
    ///         两段都是 smoothstep，正午左右斜率都为 0，所以亮度是"平顶"而不是尖峰。
    ///   购物：什么都不改，保持黄昏的样子（购物阶段永远接在白天结束之后）。
    ///
    /// 【阶段交界的连续性】白天结束在"晚"这个方向，夜晚按早 → 中 → 晚推进，所以
    /// 夜晚开始的那一刻会从"晚"跳回"早"（也就是 180° 方向翻转）。这个跳变被 NightManager
    /// 的黑屏过场盖住了，看不见；但夜晚 → 白天没有黑屏，那一下会露出来，由 _blendSpeed
    /// 抹成约半秒的快速翻转。想彻底无缝就勾上 _nightReverseArc，让夜间走反向弧线。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class TimeOfDay : MonoBehaviour
    {
        #region 场景引用

        [Header("数据源")]
        [Tooltip("阶段推进的来源。留空则自动在场景里找 GamePhaseManager")]
        [SerializeField] private GamePhaseManager _phaseManager;

        #endregion

        #region 角度

        [Header("角度（只改 X，Y/Z 用 Transform 上配好的值）")]
        [Tooltip("早晨（东）的 X：小于 90 的一侧，太阳贴地平线，影子最长")]
        [SerializeField] private float _xAtMorning = 25f;

        [Tooltip("正午的 X：90 = 正对头顶，影子最短。早 / 晚两个值要分列 90 两侧才会有方位翻转")]
        [SerializeField] private float _xAtNoon = 90f;

        [Tooltip("黄昏（西）的 X：大于 90 的一侧，和早晨同高度角、相反方向")]
        [SerializeField] private float _xAtDusk = 155f;

        [Tooltip("夜间走反向弧线（月升在白天落下的那一侧）。开启后夜晚→白天不会出现方向翻转，" +
                 "但夜间转向和白天相反；默认关 = 夜间也按东升西落推进")]
        [SerializeField] private bool _nightReverseArc;

        #endregion

        #region 白天颜色 / 强度

        [Header("白天颜色")]
        [Tooltip("早晨（刚进入白天）的颜色")]
        [SerializeField] private Color _colorMorning = Color.white;

        [Tooltip("正午的颜色")]
        [SerializeField] private Color _colorNoon = Color.white;

        [Tooltip("黄昏（白天结束时）的颜色：橙黄")]
        [SerializeField] private Color _colorDusk = new Color(1f, 0.62f, 0.25f);

        [Header("白天强度")]
        [Tooltip("早晨的强度")]
        [SerializeField, Min(0f)] private float _intensityMorning = 0.9f;

        [Tooltip("正午的强度：一天里最高")]
        [SerializeField, Min(0f)] private float _intensityNoon = 1.7f;

        [Tooltip("黄昏的强度")]
        [SerializeField, Min(0f)] private float _intensityDusk = 0.8f;

        #endregion

        #region 夜晚

        [Header("夜晚")]
        [Tooltip("夜间的颜色：青蓝冷色调")]
        [SerializeField] private Color _colorNight = new Color(0.42f, 0.66f, 1f);

        [Tooltip("夜间的强度：比白天低")]
        [SerializeField, Min(0f)] private float _intensityNight = 0.35f;

        #endregion

        #region 过渡

        [Header("过渡")]
        [Tooltip("颜色/强度/角度的平滑速度（1/秒），越大越跟手。0 = 完全瞬时切换")]
        [SerializeField, Min(0f)] private float _blendSpeed = 4f;

        #endregion

        /// <summary>
        /// X 的可用范围。取开区间 (0, 180) 是因为这个区间里 −sin x 恒为负，
        /// 光永远从上方打来；出了这个区间就是从地底照上来了。
        /// </summary>
        private const float MinAngle = 1f;
        private const float MaxAngle = 179f;

        /// <summary>方向光组件</summary>
        private Light _light;

        /// <summary>挂载时配好的 Y / Z 欧拉角，之后一直用它们。</summary>
        private float _baseY;
        private float _baseZ;

        // ---- 平滑用：当前实际应用在灯上的值 ----
        private float _appliedX;
        private Color _appliedColor;
        private float _appliedIntensity;

        /// <summary>是否已经应用过至少一次。没应用过时直接取目标值，避免从场景里的旧颜色慢慢爬过去。</summary>
        private bool _hasApplied;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null && _light.type != LightType.Directional)
            {
                Debug.LogWarning($"[TimeOfDay] {name} 上的 Light 不是方向光，角度映射不成立。", this);
            }

            // 记下配置好的 Y / Z。注意：方向光的 forward 与 Z 无关，Z 只影响 up/right，
            // 所以这里原样保留 Z 就等于"原样保留美术在 Inspector 里摆的朝向"。
            Vector3 euler = transform.localEulerAngles;
            _baseY = euler.y;
            _baseZ = euler.z;

            // 早 / 晚都在 90 的同一侧 → 只改 X 做不出方位翻转，只会让高度角来回伸缩。
            // 这是最容易配错的一处，所以给一句提示。
            if ((_xAtMorning - 90f) * (_xAtDusk - 90f) > 0f)
            {
                Debug.LogWarning(
                    $"[TimeOfDay] 早晨 {_xAtMorning}° 和黄昏 {_xAtDusk}° 都在 90° 的同一侧，" +
                    "光不会从东方翻到西方，只是高度角在伸缩。想让早晚方向相反，两个值要分列 90° 两侧。", this);
            }
        }

        private void OnEnable()
        {
            _hasApplied = false;
        }

        private void Update()
        {
            if (_light == null)
            {
                return;
            }

            if (_phaseManager == null)
            {
                _phaseManager = FindFirstObjectByType<GamePhaseManager>();
                if (_phaseManager == null)
                {
                    return; // 场景里还没就绪（或者这个场景根本没有阶段管理），下一帧再试
                }
            }

            if (!TryEvaluate(out float targetX, out Color targetColor, out float targetIntensity))
            {
                return;
            }

            Apply(targetX, targetColor, targetIntensity, Time.deltaTime);
        }

        /// <summary>
        /// 按当前阶段算出这一帧"应该"的角度 / 颜色 / 强度。
        /// 返回 false = 这一帧什么都不该做（比如购物阶段还没进入过任何阶段）。
        /// </summary>
        private bool TryEvaluate(out float x, out Color color, out float intensity)
        {
            switch (_phaseManager.CurrentPhase)
            {
                case GamePhase.Day:
                {
                    // DayProcess 是剩余比例（1 → 0），反一下才是"过了多少"（0 = 早晨）
                    float t = Mathf.Clamp01(1f - _phaseManager.DayProcess);

                    x = AngleAt(t);

                    if (t < 0.5f)
                    {
                        // 早晨 → 正午
                        float k = Smoothstep01(t * 2f);
                        color = Color.Lerp(_colorMorning, _colorNoon, k);
                        intensity = Mathf.Lerp(_intensityMorning, _intensityNoon, k);
                    }
                    else
                    {
                        // 正午 → 黄昏
                        float k = Smoothstep01((t - 0.5f) * 2f);
                        color = Color.Lerp(_colorNoon, _colorDusk, k);
                        intensity = Mathf.Lerp(_intensityNoon, _intensityDusk, k);
                    }

                    return true;
                }

                case GamePhase.Night:
                {
                    // 同样是剩余比例：0 = 刚入夜，1 = 天快亮。
                    // 默认也和白天一样按 早 → 中 → 晚 推进；反向模式则是 晚 → 中 → 早，
                    // 好处是两端都和白天接得上（入夜接着白天的"晚"，天亮接着白天的"早"）。
                    float t = Mathf.Clamp01(1f - _phaseManager.NightProcess);
                    if (_nightReverseArc)
                    {
                        t = 1f - t;
                    }

                    x = AngleAt(t);
                    color = _colorNight;
                    intensity = _intensityNight;
                    return true;
                }

                default:
                {
                    // 购物阶段：保持上一帧的样子（购物永远接在白天结束之后，所以停在黄昏）
                    x = _appliedX;
                    color = _appliedColor;
                    intensity = _appliedIntensity;
                    return _hasApplied;
                }
            }
        }

        /// <summary>
        /// 进度 t（0 = 早，0.5 = 中，1 = 晚）对应的 X 轴角度。
        /// 三段关键值之间用 smoothstep：正午处斜率为 0，光会在头顶附近"停"一下再翻向。
        /// 结果夹在 (0, 180) 内，保证光始终从上方打来。
        /// </summary>
        private float AngleAt(float t)
        {
            t = Mathf.Clamp01(t);

            float x;
            if (t < 0.5f)
            {
                float k = Smoothstep01(t * 2f);
                x = Mathf.Lerp(_xAtMorning, _xAtNoon, k);
            }
            else
            {
                float k = Smoothstep01((t - 0.5f) * 2f);
                x = Mathf.Lerp(_xAtNoon, _xAtDusk, k);
            }

            return Mathf.Clamp(x, MinAngle, MaxAngle);
        }

        /// <summary>
        /// 把目标值平滑地写进灯。指数平滑是帧率无关的：
        /// k = 1 − e^(−speed·dt)，dt 变化时曲线形状不变。
        /// </summary>
        private void Apply(float targetX, Color targetColor, float targetIntensity, float dt)
        {
            if (!_hasApplied || _blendSpeed <= 0f)
            {
                _appliedX = targetX;
                _appliedColor = targetColor;
                _appliedIntensity = targetIntensity;
            }
            else
            {
                float k = 1f - Mathf.Exp(-_blendSpeed * dt);
                _appliedX = Mathf.Lerp(_appliedX, targetX, k);
                _appliedColor = Color.Lerp(_appliedColor, targetColor, k);
                _appliedIntensity = Mathf.Lerp(_appliedIntensity, targetIntensity, k);
            }

            _hasApplied = true;

            // 只覆盖 X，Y/Z 用挂载时记下来的值
            transform.localRotation = Quaternion.Euler(_appliedX, _baseY, _baseZ);
            _light.color = _appliedColor;
            _light.intensity = _appliedIntensity;
        }

        /// <summary>0..1 的平滑插值曲线，两端斜率为 0。</summary>
        private static float Smoothstep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
