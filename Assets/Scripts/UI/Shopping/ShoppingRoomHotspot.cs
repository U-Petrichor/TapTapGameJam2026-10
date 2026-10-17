using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using TalentTree;

namespace TapTapGameJam.UI
{
    /// <summary>固定命中区域与可缩放画面分离，避免悬停边缘抖动。</summary>
    public sealed class ShoppingRoomHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image _visual;
        [SerializeField] private Sprite _normal;
        [SerializeField] private Sprite _hover;
        [SerializeField, Min(1f)] private float _hoverScale = 1.05f;
        [SerializeField, Min(0f)] private float _scaleDuration = 0.12f;
        [SerializeField] private UnityEvent _onClick = new UnityEvent();
        [SerializeField] private TalentTreeUI _modalTalentTree;
        public UnityEvent OnClick => _onClick;
        private bool CanInteract => _interactive && isActiveAndEnabled &&
            (_modalTalentTree == null || !_modalTalentTree.IsOpen);
        private bool _interactive;
        private bool _hovered;
        private float _fromScale = 1f;
        private float _targetScale = 1f;
        private float _elapsed;

        public void SetInteractionEnabled(bool enabled)
        {
            _interactive = enabled;
            ResetVisual();
        }

        private void OnDisable() => ResetVisual();

        private void ResetVisual()
        {
            _hovered = false;
            _fromScale = _targetScale = 1f;
            _elapsed = 0f;
            if (_visual == null) return;
            _visual.sprite = _normal;
            _visual.rectTransform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData evt)
        {
            if (CanInteract) SetHovered(true);
        }

        public void OnPointerClick(PointerEventData evt)
        {
            if (CanInteract && evt != null && evt.button == PointerEventData.InputButton.Left)
            {
                int feedback = TapTapGameJam.Audio.Sfx.FeedbackSequence;
                _onClick.Invoke();
                if (feedback == TapTapGameJam.Audio.Sfx.FeedbackSequence)
                    TapTapGameJam.Audio.Sfx.Play(TapTapGameJam.Audio.SoundId.Click, this);
            }
        }

        public void OnPointerExit(PointerEventData evt) => SetHovered(false);

        private void SetHovered(bool hovered)
        {
            if (_visual == null || _hovered == hovered) return;
            _hovered = hovered;
            _visual.sprite = hovered ? _hover : _normal;
            _fromScale = _visual.rectTransform.localScale.x;
            _targetScale = hovered ? _hoverScale : 1f;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_hovered && !CanInteract) SetHovered(false);
            if (_visual == null) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = _scaleDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, _elapsed / _scaleDuration);
            _visual.rectTransform.localScale = Vector3.one * Mathf.Lerp(_fromScale, _targetScale, t);
        }
    }
}
