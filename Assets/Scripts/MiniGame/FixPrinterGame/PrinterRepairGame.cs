using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class PrinterRepairGame : MiniGameContent
    {
        [SerializeField] private RectTransform _bar, _successZone, _pointer, _printerBody;
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField, Min(1)] private int _requiredHits = 5;
        [Tooltip("归一化横条长度每秒，0.65 对应单程约 1.5 秒。")]
        [SerializeField, Min(0.05f)] private float _pointerSpeed = 0.65f;
        [SerializeField, Range(0.05f, 1f)] private float _successWidth = 0.25f;
        [Header("敲击与时机反馈")]
        [SerializeField] private RectTransform _hand, _handFall;
        [SerializeField] private Image _flash, _flashWrongTime;
        [SerializeField, Min(0.01f)] private float _handDownDuration = 0.1f;
        [SerializeField, Min(0f)] private float _handHoldDuration = 0.03f;
        [SerializeField, Min(0.01f)] private float _handReturnDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.08f;
        private PrinterRepairRound _round;
        private Action _onCompleted;
        private CanvasGroup _panelGroup;
        private Vector2 _printerOrigin;
        private float _feedbackSeconds, _shakeSeconds;
        private bool _running;
        private Vector3 _handOrigin, _handTarget, _handScale, _handTargetScale;
        private Quaternion _handRotation, _handTargetRotation;
        private Color _flashColor, _wrongFlashColor;
        private Image _capturedFlash, _capturedWrongFlash;
        private bool _pointerPoseCaptured;
        private float _pointerY, _pointerAnchorY;
        private bool _handPoseCaptured, _impactPlayed, _completionPending;
        private float _strikeTime = -1f;
        private readonly Queue<PrinterHitResult> _queuedStrikes = new();
        private PrinterHitResult _strikeResult;
        private bool HasHandAnimation => _hand != null && _handFall != null;
        private bool HasStrikeVisuals => HasHandAnimation || _flash != null || _flashWrongTime != null;

        public bool IsConfigured => _bar != null && _successZone != null && _pointer != null && _printerBody != null;
        public void Configure(RectTransform bar, RectTransform zone, RectTransform pointer, RectTransform printer,
            TMP_Text feedback)
        {
            _bar = bar; _successZone = zone; _pointer = pointer; _printerBody = printer;
            _feedbackText = feedback;
            _pointerPoseCaptured = false;
        }
        public override void Begin(Action onCompleted)
        {
            ResetView();
            if (!IsConfigured) throw new InvalidOperationException("请配置打印机 QTE 的横条、黄色区域、指针与打印机图片。");
            _panelGroup = GetComponentInParent<MiniGamePanelUI>().GetComponent<CanvasGroup>();
            _printerOrigin = _printerBody.anchoredPosition;
            CaptureHandPose();
            ResetStrike();
            _round = new PrinterRepairRound(Mathf.Max(1, _requiredHits), Mathf.Max(0.05f, _pointerSpeed),
                Mathf.Clamp(_successWidth, 0.05f, 1f), () => UnityEngine.Random.value);
            _onCompleted = onCompleted;
            _running = true;
            Render();
        }
        private void Update()
        {
            if (!_running || _panelGroup == null || !_panelGroup.interactable) return;
            AdvanceStrike(Time.unscaledDeltaTime);
            if (!_running) return;
            _round.Tick(Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Hit();
            if (!_running) return;
            _feedbackSeconds -= Time.deltaTime;
            _shakeSeconds = Mathf.Max(0, _shakeSeconds - Time.deltaTime);
            _printerBody.anchoredPosition = _printerOrigin + Vector2.right *
                (_shakeSeconds > 0 ? Mathf.Sin(_shakeSeconds * 90) * 7 * (_shakeSeconds / 0.2f) : 0);
            if (_feedbackSeconds <= 0 && !_completionPending) SetFeedback("等待指针进入黄色区域，再按空格", new Color(0.1f, 0.17f, 0.24f));
            Render();
        }
        // 只响应独立按下，长按空格不会自动增加次数。
        public void Hit()
        {
            if (!_running || _completionPending || !isActiveAndEnabled || _panelGroup == null || !_panelGroup.interactable) return;
            var outcome = _round.Hit();
            if (outcome == PrinterHitResult.Ignored) return;
            _feedbackSeconds = 0.7f;
            if (HasStrikeVisuals)
            {
                // 保存按下当时的结果，排队播放时不再按移动后的指针重新判定。
                if (_strikeTime >= 0f) _queuedStrikes.Enqueue(outcome);
                else StartStrike(outcome);
            }
            else _shakeSeconds = 0.2f;
            SetFeedback(outcome == PrinterHitResult.Miss ? "时机不对" : "敲打有效！",
                outcome == PrinterHitResult.Miss ? new Color(0.72f, 0.19f, 0.16f) : new Color(0.1f, 0.48f, 0.3f));
            Render();
            if (outcome != PrinterHitResult.Complete) return;
            SetFeedback("打印机恢复正常！", new Color(0.1f, 0.48f, 0.3f));
            _completionPending = true;
            if (_strikeTime < 0f) CompleteRound();
        }

        private void CaptureHandPose()
        {
            CaptureFlashColors();
            if (!HasHandAnimation || _handPoseCaptured) return;
            _handOrigin = _hand.localPosition;
            _handRotation = _hand.localRotation;
            _handScale = _hand.localScale;
            // HandFall 只提供目标姿态，不参与实际渲染；支持不同父级。
            _handTarget = _hand.parent.InverseTransformPoint(_handFall.position);
            _handTargetRotation = Quaternion.Inverse(_hand.parent.rotation) * _handFall.rotation;
            _handTargetScale = _handFall.localScale;
            foreach (var graphic in _hand.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var graphic in _handFall.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            _handPoseCaptured = true;
        }

        private void StartStrike(PrinterHitResult result)
        {
            _strikeTime = 0f;
            _impactPlayed = false;
            _strikeResult = result;
            HideFlashes();
        }

        private void AdvanceStrike(float seconds)
        {
            if (_strikeTime < 0f) return;
            _strikeTime += Mathf.Max(0f, seconds);
            float down = Mathf.Max(0.01f, _handDownDuration);
            float hold = Mathf.Max(0f, _handHoldDuration);
            float up = Mathf.Max(0.01f, _handReturnDuration);
            float flash = Mathf.Max(0.01f, _flashDuration);
            if (_strikeTime >= down && !_impactPlayed)
            {
                _impactPlayed = true;
                _shakeSeconds = 0.2f;
                // 到达接触姿态的这一帧必须显示满亮度，避免低帧率直接跳过短闪光。
                _strikeTime = down;
            }
            float amount = _strikeTime < down ? Mathf.SmoothStep(0f, 1f, _strikeTime / down)
                : 1f - Mathf.SmoothStep(0f, 1f, (_strikeTime - down - hold) / up);
            if (HasHandAnimation)
            {
                _hand.localPosition = Vector3.Lerp(_handOrigin, _handTarget, amount);
                _hand.localRotation = Quaternion.Slerp(_handRotation, _handTargetRotation, amount);
                _hand.localScale = Vector3.Lerp(_handScale, _handTargetScale, amount);
            }
            float alpha = _strikeTime >= down ? Mathf.Clamp01(1f - (_strikeTime - down) / flash) : 0f;
            bool wrong = _strikeResult == PrinterHitResult.Miss;
            SetFlash(_flash, _flashColor, wrong ? 0f : alpha);
            SetFlash(_flashWrongTime, _wrongFlashColor, wrong ? alpha : 0f);
            if (_strikeTime < down + Mathf.Max(hold + up, flash)) return;
            RestoreHand();
            _strikeTime = -1f;
            if (_queuedStrikes.Count > 0) StartStrike(_queuedStrikes.Dequeue());
            else if (_completionPending) CompleteRound();
        }

        private void RestoreHand()
        {
            if (_handPoseCaptured && _hand != null)
            {
                _hand.localPosition = _handOrigin;
                _hand.localRotation = _handRotation;
                _hand.localScale = _handScale;
                _hand.gameObject.SetActive(true);
            }
            if (_handFall != null) _handFall.gameObject.SetActive(false);
            HideFlashes();
        }

        private static void SetFlash(Image image, Color original, float alpha)
        {
            if (image == null) return;
            original.a *= alpha;
            image.color = original;
            if (alpha > 0f)
            {
                image.enabled = true;
                if (!image.gameObject.activeSelf) image.transform.SetAsLastSibling();
            }
            image.gameObject.SetActive(alpha > 0f);
        }

        private void CaptureFlashColors()
        {
            // 兼容旧场景没有保存新字段，或在首次打开后才补绑错误闪光。
            if (_flash == null) _flash = transform.Find("Flash")?.GetComponent<Image>();
            if (_flashWrongTime == null) _flashWrongTime = transform.Find("FlashWrongTime")?.GetComponent<Image>();
            if (_flash != null && _capturedFlash != _flash)
            {
                _capturedFlash = _flash;
                _flashColor = VisibleFlashColor(_flash);
            }
            if (_flashWrongTime != null && _capturedWrongFlash != _flashWrongTime)
            {
                _capturedWrongFlash = _flashWrongTime;
                _wrongFlashColor = VisibleFlashColor(_flashWrongTime);
            }
        }

        private static Color VisibleFlashColor(Image image)
        {
            image.raycastTarget = false;
            Color color = image.color;
            // 闪光由动画隐藏，零透明度的初始/重载状态不能成为永久不可见的基准。
            if (color.a <= 0f) color.a = 1f;
            return color;
        }

        private void HideFlashes()
        {
            CaptureFlashColors();
            if (_flash != null)
            {
                _flash.gameObject.SetActive(false);
                _flash.color = _flashColor;
            }
            if (_flashWrongTime != null)
            {
                _flashWrongTime.gameObject.SetActive(false);
                _flashWrongTime.color = _wrongFlashColor;
            }
        }

        private void ResetStrike()
        {
            _strikeTime = -1f; _queuedStrikes.Clear();
            _impactPlayed = _completionPending = false;
            RestoreHand();
        }

        private void CompleteRound()
        {
            _running = false;
            _completionPending = false;
            var completed = _onCompleted; _onCompleted = null;
            completed?.Invoke();
        }
        private void Render()
        {
            if (_round == null) return;
            // anchor 使用归一化位置，RectTransform 尺寸变化后判定仍与画面一致。
            _successZone.anchorMin = new Vector2((float)_round.ZoneStart, 0);
            _successZone.anchorMax = new Vector2((float)(_round.ZoneStart + _round.ZoneWidth), 1);
            _successZone.offsetMin = _successZone.offsetMax = Vector2.zero;
            PositionPointer((float)_round.Position);
        }

        private void PositionPointer(float position)
        {
            if (_pointer == null) return;
            if (!_pointerPoseCaptured)
            {
                _pointerY = _pointer.anchoredPosition.y;
                _pointerAnchorY = (_pointer.anchorMin.y + _pointer.anchorMax.y) * 0.5f;
                _pointerPoseCaptured = true;
            }
            _pointer.anchorMin = _pointer.anchorMax = new Vector2(position, _pointerAnchorY);
            _pointer.anchoredPosition = new Vector2(0f, _pointerY);
        }
        private void SetFeedback(string message, Color color)
        {
            if (_feedbackText != null) { _feedbackText.text = message; _feedbackText.color = color; }
        }
        public override void End()
        {
            _running = false; _onCompleted = null; _round?.Stop();
            if (_printerBody != null && _round != null) _printerBody.anchoredPosition = _printerOrigin;
            _feedbackSeconds = _shakeSeconds = 0;
            ResetStrike();
        }
        public override void ResetView()
        {
            End(); _round = null;
            SetFeedback("等待指针进入黄色区域，再按空格", new Color(0.1f, 0.17f, 0.24f));
            PositionPointer(0f);
        }
        private void OnDisable() => End();
        private void OnDestroy() => End();
    }
}
