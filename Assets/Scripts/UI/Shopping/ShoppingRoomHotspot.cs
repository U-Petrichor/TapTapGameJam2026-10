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
        [SerializeField] private Image _outline;
        [SerializeField] private Sprite _normal;
        [SerializeField] private Sprite _hover;
        [SerializeField, Min(1f)] private float _hoverScale = 1.05f;
        [SerializeField, Min(0f)] private float _scaleDuration = 0.12f;
        [SerializeField] private UnityEvent _onClick = new UnityEvent();
        [SerializeField] private TalentTreeUI _modalTalentTree;

        [Tooltip("悬停时显示、移开时隐藏的提示按钮。初始化（以及失焦 / 停用）时自动隐藏")]
        [SerializeField] private Transform _hintButton;
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

        /// <summary>初始化就把提示藏起来：场景里摆着它是为了方便对齐，运行时只在悬停时露出来</summary>
        private void Awake() => SetHintVisible(false);

        private void OnDisable() => ResetVisual();

        private void ResetVisual()
        {
            _hovered = false;
            _fromScale = _targetScale = 1f;
            _elapsed = 0f;
            if (_outline != null) _outline.enabled = false;
            SetHintVisible(false);
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
            if (_hovered == hovered) return;
            _hovered = hovered;

            // 提示按钮的显隐不依赖 _visual：_visual 没配也照样该露提示
            SetHintVisible(hovered);

            if (_visual == null) return;
            if (_outline != null) _outline.enabled = hovered && _outline.sprite != null;
            _visual.sprite = hovered && _hover != null ? _hover : _normal;
            _fromScale = _visual.rectTransform.localScale.x;
            _targetScale = hovered ? _hoverScale : 1f;
            _elapsed = 0f;
        }

        /// <summary>提示按钮显隐。只切 active，不动设计师摆的位置 / 缩放</summary>
        private void SetHintVisible(bool visible)
        {
            if (_hintButton == null)
            {
                return;
            }

            if (_hintButton.gameObject.activeSelf != visible)
            {
                _hintButton.gameObject.SetActive(visible);
            }
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
