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
        [SerializeField] private string _emptyColor = "#9A9A9A";

        [Tooltip("有蟑螂时的颜色")]
        [SerializeField] private string _countColor = "#8CE05A";

        [Tooltip("孵化指令的颜色")]
        [SerializeField] private string _hatchColor = "#E0C05A";

        [Tooltip("觅食指令的颜色")]
        [SerializeField] private string _foragingColor = "#5AC8E0";

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
            string countColor = count > 0 ? _countColor : _emptyColor;

            _text.text =
                $"<color={countColor}>{count}</color> cockroaches\n" +
                $"<size=70%><color={CommandColor(_nest.CurrentCommand)}>{CommandLabel(_nest.CurrentCommand)}</color></size>";
        }

        private string CommandColor(CockroachCommand command)
        {
            return command == CockroachCommand.Hatch ? _hatchColor : _foragingColor;
        }

        private static string CommandLabel(CockroachCommand command)
        {
            return command == CockroachCommand.Hatch ? "Command: Hatch" : "Command: Forging";
        }
    }
}
