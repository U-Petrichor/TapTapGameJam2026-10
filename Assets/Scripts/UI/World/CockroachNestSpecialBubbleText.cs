using TapTapGameJam.Cockroach;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 巢穴头顶的气泡提示文字：跟着阶段和巢穴状态换文案。
    ///
    /// - 未标记（<see cref="CockNest.NestLockState.UnFounded"/>）：白天显示"标记巢穴"。
    ///   此时还搬不动，所以不能显示"搬运"；夜里干脆什么都不显示（这个阶段无法交互）。
    /// - 白天其余状态：搬运。
    /// - 夜里：Locked 产卵、FirstHatching 孵化、其余留空。
    /// </summary>
    public class CockroachNestSpecialBubbleText: MonoBehaviour
    {
        TextMeshProUGUI _textMeshProUGUI;

        CockNest _nest;

        private void Awake()
        {
            _nest = GetComponentInParent<CockNest>();
            _textMeshProUGUI = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (_nest == null || _textMeshProUGUI == null) return;

            bool isDay = GameManager.IsDay;


            if (isDay)
            {
                if (_nest.nestLockState == CockNest.NestLockState.UnFounded)
                {
                    _textMeshProUGUI.text = "标记";
                    return;
                }
                _textMeshProUGUI.text = "搬运";
                return;
            }

            if (_nest.nestLockState == CockNest.NestLockState.UnFounded) _textMeshProUGUI.text = "标记";
            else if (_nest.nestLockState == CockNest.NestLockState.Locked) _textMeshProUGUI.text = "产卵";
            else if (_nest.nestLockState == CockNest.NestLockState.FirstHatching) _textMeshProUGUI.text = "孵化";
            else _textMeshProUGUI.text = string.Empty;
        }
    }
}
