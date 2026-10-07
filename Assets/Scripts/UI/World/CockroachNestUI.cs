using TapTapGameJam.Cockroach;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 挂在 World Space Canvas 上，每帧读 CockNest 的蟑螂数量、当前指令和孵化进度。
    ///
    /// 孵化进度环的 Image 需要在 Inspector 里设成：
    ///     Image Type = Filled, Fill Method = Radial 360, Fill Origin = Top, Clockwise
    /// 不设的话 fillAmount 不会生效（图还是整圆）。它每孵出一只转满一圈，
    /// 并且只在 Hatch 指令下显示。
    /// </summary>
    [DisallowMultipleComponent]
    public class CockroachNestUI : MonoBehaviour
    {
        [Tooltip("文本")]
        [SerializeField] private TMP_Text _text;

        [Tooltip("孵化进度环。留空则不显示进度")]
        [SerializeField] private Image _hatchProgressRing;

        [Tooltip("要显示的巢穴。留空则从自己往上找")]
        [SerializeField] private CockNest _nest;

        [Header("富文本颜色")]
        [Tooltip("蟑螂数量为 0 时的颜色")]
        [SerializeField] private Color _emptyColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Tooltip("有蟑螂时的颜色")]
        [SerializeField] private Color _countColor = new Color(0.55f, 0.88f, 0.35f, 1f);

        private void Awake()
        {
            if (_nest == null)
            {
                _nest = GetComponentInParent<CockNest>();
            }

            if (_nest == null)
            {
                Debug.LogError($"[CockroachNestUI:{name}]：没找到 CockNest，也没手动指定");
            }

            if (_text == null)
            {
                Debug.LogError($"[CockroachNestUI:{name}]：没有配置文本");
            }
        }

        private void Update()
        {
            if (_nest == null || _text == null)
            {
                return;
            }

            int count = _nest.CockroachList.Count;
            string countColor = ToRichTextColor(count > 0 ? _countColor : _emptyColor);

            _text.text =
                $"<color={countColor}>{count}</color> cockroaches\n" +
                $"<size=70%><color={CommandColor(_nest.CurrentCommand)}>{CommandLabel(_nest.CurrentCommand)}</color></size>";

            RefreshHatchRing();
        }

        /// <summary>孵化进度环：只在 Hatch 指令下显示，fillAmount 就是进度</summary>
        private void RefreshHatchRing()
        {
            if (_hatchProgressRing == null)
            {
                return;
            }

            bool show = _nest.CurrentCommand == CockroachCommand.Hatch;
            if (_hatchProgressRing.enabled != show)
            {
                _hatchProgressRing.enabled = show;
            }

            if (show)
            {
                _hatchProgressRing.fillAmount = _nest.HatchProgress;
            }
        }

        /// <summary>
        /// 指令颜色。源头在 CockNest.HatchColor / ForagingColor，
        /// 范围虚线圈用的是同一份，不会再各自漂移。
        /// </summary>
        private static string CommandColor(CockroachCommand command)
        {
            return ToRichTextColor(command == CockroachCommand.Hatch
                ? CockNest.HatchColor
                : CockNest.ForagingColor);
        }

        /// <summary>转成富文本能吃的 #RRGGBB</summary>
        private static string ToRichTextColor(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

        private static string CommandLabel(CockroachCommand command)
        {
            return command == CockroachCommand.Hatch ? "Command: Hatch" : "Command: Forging";
        }
    }
}
