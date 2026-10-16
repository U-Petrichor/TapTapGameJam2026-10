using System.Collections;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.Core
{
    /// <summary>Shopping → Night 的提示文字、黑屏切换和控制恢复。</summary>
    public sealed class NightManager : MonoBehaviour
    {
        [SerializeField] private ShoppingManager _shoppingManager;
        [SerializeField] private GamePhaseManager _phaseManager;
        [SerializeField] private GameObject _fadeOverlayCanvas;
        [SerializeField] private Image _fadeImage;
        [SerializeField] private TMP_Text _fadeText;
        [SerializeField] private string _message = "潜入公司！";
        [SerializeField, Min(0f)] private float _textDisplayDuration = 0.5f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;
        private Coroutine _transition;

        private void OnEnable() => EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
            if (_transition == null) return;
            StopCoroutine(_transition);
            _transition = null;
            if (_shoppingManager != null) _shoppingManager.CompleteNightTransition();
            if (_fadeOverlayCanvas != null) _fadeOverlayCanvas.SetActive(false);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase != GamePhase.Night)
            {
                if (_transition == null) return;
                StopCoroutine(_transition);
                _transition = null;
                // ShoppingManager 已处理新阶段；不能覆盖刚启动的 Shopping 过场。
                if (evt.NewPhase != GamePhase.Shopping && _fadeOverlayCanvas != null)
                    _fadeOverlayCanvas.SetActive(false);
                return;
            }
            if (_transition != null) return;
            if (_shoppingManager == null || _phaseManager == null || _fadeOverlayCanvas == null ||
                _fadeImage == null || _fadeText == null)
            {
                Debug.LogError("[NightManager] 请配置阶段管理器、ShoppingManager 和过场 UI。", this);
                return;
            }
            _phaseManager.TimeCouldWork = false;
            _shoppingManager.BeginNightTransition();
            _transition = StartCoroutine(EnterNight());
        }

        private IEnumerator EnterNight()
        {
            SetAlpha(0f);
            _fadeText.text = _message;
            _fadeText.gameObject.SetActive(true);
            _fadeOverlayCanvas.SetActive(true);
            yield return new WaitForSecondsRealtime(_textDisplayDuration);
            yield return Fade(0f, 1f);

            _fadeText.gameObject.SetActive(false);
            _shoppingManager.HideRoomForNight();
            Canvas.ForceUpdateCanvases();
            yield return Fade(1f, 0f);

            _fadeOverlayCanvas.SetActive(false);
            _shoppingManager.CompleteNightTransition();
            _transition = null;
            EventPipeline.Publish(new NightTransitionCompletedEvent());
        }

        private IEnumerator Fade(float from, float to)
        {
            SetAlpha(from);
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / _fadeDuration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha) => _fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }
}
