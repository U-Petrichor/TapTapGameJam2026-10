using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class PrinterRepairGame : MiniGameContent
    {
        [SerializeField] private RectTransform _bar, _successZone, _pointer, _printerBody;
        [SerializeField] private TMP_Text _progressText, _feedbackText;
        [SerializeField] private Slider _progressBar;
        [SerializeField, Min(1)] private int _requiredHits = 5;
        [Tooltip("归一化横条长度每秒，0.65 对应单程约 1.5 秒。")]
        [SerializeField, Min(0.05f)] private float _pointerSpeed = 0.65f;
        [SerializeField, Range(0.05f, 1f)] private float _successWidth = 0.25f;
        private PrinterRepairRound _round;
        private Action _onCompleted;
        private CanvasGroup _panelGroup;
        private Vector2 _printerOrigin;
        private float _feedbackSeconds, _shakeSeconds;
        private bool _running;

        public bool IsConfigured => _bar != null && _successZone != null && _pointer != null && _printerBody != null;
        public void Configure(RectTransform bar, RectTransform zone, RectTransform pointer, RectTransform printer,
            TMP_Text progressText, TMP_Text feedback, Slider progressBar)
        {
            _bar = bar; _successZone = zone; _pointer = pointer; _printerBody = printer;
            _progressText = progressText; _feedbackText = feedback; _progressBar = progressBar;
        }
        public override void Begin(Action onCompleted)
        {
            ResetView();
            if (!IsConfigured) throw new InvalidOperationException("请配置打印机 QTE 的横条、黄色区域、指针与打印机图片。");
            _panelGroup = GetComponentInParent<MiniGamePanelUI>().GetComponent<CanvasGroup>();
            _printerOrigin = _printerBody.anchoredPosition;
            _round = new PrinterRepairRound(Mathf.Max(1, _requiredHits), Mathf.Max(0.05f, _pointerSpeed),
                Mathf.Clamp(_successWidth, 0.05f, 1f), () => UnityEngine.Random.value);
            _onCompleted = onCompleted;
            _running = true;
            Render();
        }
        private void Update()
        {
            if (!_running || _panelGroup == null || !_panelGroup.interactable) return;
            _round.Tick(Time.deltaTime);
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Hit();
            if (!_running) return;
            _feedbackSeconds -= Time.deltaTime;
            _shakeSeconds = Mathf.Max(0, _shakeSeconds - Time.deltaTime);
            _printerBody.anchoredPosition = _printerOrigin + Vector2.right *
                (_shakeSeconds > 0 ? Mathf.Sin(_shakeSeconds * 90) * 7 * (_shakeSeconds / 0.2f) : 0);
            if (_feedbackSeconds <= 0) SetFeedback("等待指针进入黄色区域，再按空格", new Color(0.1f, 0.17f, 0.24f));
            Render();
        }
        // 只响应独立按下，长按空格不会自动增加次数。
        public void Hit()
        {
            if (!_running || !isActiveAndEnabled || _panelGroup == null || !_panelGroup.interactable) return;
            var outcome = _round.Hit();
            if (outcome == PrinterHitResult.Ignored) return;
            _shakeSeconds = 0.2f; _feedbackSeconds = 0.7f;
            SetFeedback(outcome == PrinterHitResult.Miss ? "时机不对 · 已完成次数保留" : "敲打有效！",
                outcome == PrinterHitResult.Miss ? new Color(0.72f, 0.19f, 0.16f) : new Color(0.1f, 0.48f, 0.3f));
            Render();
            if (outcome != PrinterHitResult.Complete) return;
            SetFeedback("打印机恢复正常！", new Color(0.1f, 0.48f, 0.3f));
            _running = false;
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
            _pointer.anchorMin = _pointer.anchorMax = new Vector2((float)_round.Position, 0.5f);
            _pointer.anchoredPosition = Vector2.zero;
            if (_progressText != null) _progressText.text = $"修复进度：{_round.Successes} / {_round.RequiredHits}";
            if (_progressBar != null) _progressBar.value = (float)_round.Successes / _round.RequiredHits;
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
        }
        public override void ResetView()
        {
            End(); _round = null;
            if (_progressText != null) _progressText.text = $"修复进度：0 / {Mathf.Max(1, _requiredHits)}";
            if (_progressBar != null) _progressBar.value = 0;
            SetFeedback("等待指针进入黄色区域，再按空格", new Color(0.1f, 0.17f, 0.24f));
            if (_pointer != null) { _pointer.anchorMin = _pointer.anchorMax = new Vector2(0, 0.5f); _pointer.anchoredPosition = Vector2.zero; }
        }
        private void OnDisable() => End();
        private void OnDestroy() => End();
    }
}
