using TapTapGameJam.Cockroach;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 挂在 World Space Canvas 上，每帧读 CockroachNest 的蟑螂数量和当前指令，刷新文本。
    /// </summary>
    [DisallowMultipleComponent]
    public class CockroachNestUI : MonoBehaviour
    {
        [Tooltip("文本")]
        [SerializeField] private TMP_Text _text;

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
