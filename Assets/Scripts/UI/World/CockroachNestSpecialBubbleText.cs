using System;
using TapTapGameJam.Cockroach;
using TapTapGameJam.Core;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
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
            if (GameManager.IsDay) _textMeshProUGUI.text = "搬运";
            else
            {
                if (_nest.nestLockState == CockNest.NestLockState.Locked) _textMeshProUGUI.text = "产卵";
                if (_nest.nestLockState == CockNest.NestLockState.FirstHatching) _textMeshProUGUI.text = "孵化";
            }
        }
    }
}