using System;
using UnityEngine.Assertions;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 体力条 UI：5 条竖条 + 4 个图标。
    /// 这个类只做两件事：<b>设 anchoredPosition.x</b> 和 <b>显隐</b>。
    /// y/z、宽度、颜色、层级、pivot、锚点全部由预制体里配好的值负责，代码一律不碰。
    ///
    /// 【数值 → x 的映射】
    /// 数值 0 映射到 xMin，数值 <see cref="_barRefValue"/> 映射到 xMax，中间线性。
    /// 默认 [0,100] → [-640,0]，所以 0 → -640、35 → -416、70 → -192、100 → 0。
    ///
    /// 【五条 bar 的位置：含自身的前缀和】
    /// 顺序：heavy → power_front → power_back → poison → damage，每一项的"位置"取它自己的右边界：
    ///     prefix(heavy)       = heavy
    ///     prefix(power_front) = heavy + powerAvailable
    ///     prefix(power_back)  = heavy + powerTotal
    ///     prefix(poison)      = heavy + powerTotal + poison
    ///     prefix(damage)      = heavy + powerTotal + poison + damage
    /// 条的"左边界" = 位置 - 自身值，也就是：
    ///     heavy       左边界 = 0
    ///     power_front 左边界 = heavy + (powerTotal - powerAvailable)   ← 压在 power_back 上
    ///     power_back  左边界 = heavy
    ///     poison      左边界 = heavy + powerTotal
    ///     damage      左边界 = heavy + powerTotal + poison
    ///
    /// 【四个 icon 的位置：不含自身的前缀和，靠左对齐】
    /// 顺序：heavy → power_front → power_back → poison → damage（damage 在最右），
    /// icon 的 x 就取"它左边那条 bar 的左边界"：
    ///     icon_heavy   前缀 = 0
    ///     icon_power   前缀 = heavy
    ///     icon_poison  前缀 = heavy + powerTotal
    ///     icon_damage  前缀 = heavy + powerTotal + poison
    /// icon 走自己的映射范围（默认 [0,80] → [-320,270]）。
    ///
    /// 【用你的例子验算】heavy=10 / powerAvailable=25 / powerTotal=30 / poison=30 / damage=30：
    ///     power_front 位置 = 10 + 25 = 35 ✓（左边界 = 10，正好接在 heavy 后面）
    ///     poison 区间 = [40, 70] ✓
    ///     power_back 区间 = [10, 40]，power_front 右侧因此露出 [35,40] 这 5 点"上限被占"的部分
    ///     icon_damage 前缀 = 10 + 30 + 30 = 70 ✓
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealthUI : MonoBehaviour
    {
        #region 五条 bar

        [Header("体力条（5 张 Image，顺序：heavy / power_front / power_back / poison / damage）")]
        [Tooltip("负重条")]
        [SerializeField] private Image _heavyBar;
        [Tooltip("可用体力：当前量（前景）")]
        [SerializeField] private Image _powerFrontBar;
        [Tooltip("可用体力：上限总量（背景），应压在 power_front 下面")]
        [SerializeField] private Image _powerBackBar;
        [Tooltip("中毒条")]
        [SerializeField] private Image _poisonBar;
        [Tooltip("受伤条")]
        [SerializeField] private Image _damageBar;

        [Header("bar 的数值 → x 映射")]
        [Tooltip("数值 0 对应的 x")]
        [SerializeField] private float _barXMin = -640f;
        [Tooltip("数值 = 参考值时对应的 x")]
        [SerializeField] private float _barXMax = 0f;
        [Tooltip("数值轴参考值：位置刚好从 _barXMin 走到 _barXMax 的那个数值")]
        [SerializeField, Min(1f)] private float _barRefValue = 100f;

        #endregion

        #region 四个 icon

        [Header("图标（4 张 Image：heavy / power / poison / damage）")]
        [SerializeField] private Image _heavyIcon;
        [SerializeField] private Image _powerIcon;
        [SerializeField] private Image _poisonIcon;
        [SerializeField] private Image _damageIcon;
        
        [Header("icon 的数值 → x 映射")]
        [Tooltip("前缀 0 对应的 x")]
        [SerializeField] private float _iconXMin = -320f;
        [Tooltip("前缀 = 参考值时对应的 x")]
        [SerializeField] private float _iconXMax = 270f;
        [Tooltip("icon 的数值轴参考值")]
        [SerializeField, Min(1f)] private float _iconRefValue = 80f;

        private CanvasGroup _canvasGroup;
        #endregion

        #region 显示规则

        [Header("显示")]
        [Tooltip("对应项大于这个值才显示 icon；小于等于则隐藏")]
        [SerializeField] private float _showIconThreshold = 15f;
        [Tooltip("对应项的值为 0 时，显式隐藏该条 Image")]
        [SerializeField] private bool _hideZeroBars = true;

        #endregion

        #region 数据源

        [Header("数据源")]
        [Tooltip("玩家体力组件。从场景里 Player 实例上拖过来；留空则运行时自动找")]
        [SerializeField] private PlayerHealth _health;

        #endregion

        private void Awake()
        {
            if (_health == null)
            {
                _health = FindFirstObjectByType<PlayerHealth>();
            }
            _canvasGroup = GetComponent<CanvasGroup>();
            Assert.IsTrue(_canvasGroup, "没有挂canvas group");
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnPhaseChanged);
        }

        void OnPhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Night)
            {
                _canvasGroup.alpha = 1;
            }
            else
            {
                _canvasGroup.alpha = 0;
            }
        }

        private void LateUpdate()
        {
            // 每帧直接读 PlayerHealth 重算，和背包界面一致：数据只有一份真相，不会显示陈旧内容
            if (_health == null)
            {
                _health = FindFirstObjectByType<PlayerHealth>();
                if (_health == null)
                {
                    return;
                }
            }

            Refresh();
        }

        /// <summary>按当前体力状态刷新所有条与图标</summary>
        public void Refresh()
        {
            float heavy = _health.Heavy;
            float powerTotal = Mathf.Max(0f, _health.PowerTotal);
            float powerAvailable = Mathf.Clamp(_health.PowerAvailable, 0f, powerTotal);
            float poison = _health.Poison;
            float damage = _health.Damage;

            // ---- 五条 bar ----
            // 每条的位置（右边界） = 含自身前缀和
            SetBar(_heavyBar, heavy, heavy);
            SetBar(_powerFrontBar, heavy + powerAvailable, powerAvailable);
            SetBar(_powerBackBar, heavy + powerTotal, powerTotal - powerAvailable);
            SetBar(_poisonBar, heavy + powerTotal + poison, poison);
            SetBar(_damageBar, heavy + powerTotal + poison + damage, damage);

            // ---- 四个 icon ----
            // 每条的 x = 不含自身的前缀和（= 它左边那条 bar 的左边界）
            SetIcon(_heavyIcon, heavy, 0f);
            SetIcon(_powerIcon, powerTotal, heavy);
            SetIcon(_poisonIcon, poison, heavy + powerTotal);
            SetIcon(_damageIcon, damage, heavy + powerTotal + poison);
        }

        private void SetBar(Image bar, float rightValue, float value)
        {
            if (bar == null)
            {
                return;
            }

            if (_hideZeroBars && value <= 0.0001f)
            {
                SetVisible(bar, false);
                return;
            }

            SetVisible(bar, true);
            SetAnchoredX(bar.rectTransform, MapValue(rightValue, _barRefValue, _barXMin, _barXMax));
        }

        /// <summary>
        /// 摆一个图标：只改 x，y/z 一律不动。
        /// value 用于判断显隐（大于阈值才显示），prefix 是不含自身的前缀和（用于定位置）。
        /// </summary>
        private void SetIcon(Image icon, float value, float prefix)
        {
            if (icon == null)
            {
                return;
            }

            // "对应项不超过 showIconThreshold 时隐藏" → 小于等于阈值就藏
            if (value <= _showIconThreshold)
            {
                SetVisible(icon, false);
                return;
            }

            SetVisible(icon, true);
            SetAnchoredX(icon.rectTransform, MapValue(prefix, _iconRefValue, _iconXMin, _iconXMax));
        }

        /// <summary>只改 anchoredPosition.x，y 和 z 原样保留</summary>
        private static void SetAnchoredX(RectTransform rect, float x)
        {
            if (rect == null)
            {
                return;
            }

            Vector2 pos = rect.anchoredPosition;
            pos.x = x;
            rect.anchoredPosition = pos;
        }

        private static void SetVisible(Image image, bool visible)
        {
            if (image != null)
            {
                // 用 enabled 而不是 SetActive：和项目里其它 UI 的做法一致
                image.enabled = visible;
            }
        }

        /// <summary>把 value 从 [0, refValue] 线性映射到 [xMin, xMax]，超出范围夹住</summary>
        private static float MapValue(float value, float refValue, float xMin, float xMax)
        {
            float t = Mathf.Clamp01(value / Mathf.Max(0.0001f, refValue));
            return Mathf.Lerp(xMin, xMax, t);
        }
    }
}
