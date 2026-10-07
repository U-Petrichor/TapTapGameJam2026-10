using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class NewInteractionSelectorUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionManager interactionManager;
        [SerializeField] private TMP_Text interactionPrompt;
        private CanvasGroup _canvasGroup;
        private bool _hasInteractionTargets;
        private bool _isPlayingMiniGame;
        private bool _isShopping;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (interactionPrompt == null)
            {
                interactionPrompt = GetComponentInChildren<TMP_Text>(true);
            }
            if (interactionManager == null)
            {
                Debug.LogError("NewInteractionSelectorUI：未配置玩家交互管理器", this);
            }
            if (interactionPrompt == null)
            {
                Debug.LogError("NewInteractionSelectorUI：未配置提示文本", this);
            }
        }

        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.EventShowInteractionUI += OnShowInteractionUI;
                interactionManager.EventHideInteractionUI += OnHideInteractionUI;
                interactionManager.EventInteractionPromptChanged += OnPromptChanged;
            }
            EventPipeline.Subscribe<GamePhaseChangedEvent>(OnGamePhaseChange);

            // UI 晚于玩家启用时，也能显示已选中的目标。
            _hasInteractionTargets = interactionManager != null && interactionManager.HasInteractionTargets;
            OnPromptChanged(interactionManager != null ? interactionManager.CurrentInteractionPrompt : string.Empty);
            RefreshVisibility();
        }

        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.EventShowInteractionUI -= OnShowInteractionUI;
                interactionManager.EventHideInteractionUI -= OnHideInteractionUI;
                interactionManager.EventInteractionPromptChanged -= OnPromptChanged;
            }
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnGamePhaseChange);
        }

        private void OnShowInteractionUI()
        {
            _hasInteractionTargets = true;
            RefreshVisibility();
        }

        private void OnHideInteractionUI()
        {
            _hasInteractionTargets = false;
            OnPromptChanged(string.Empty);
            RefreshVisibility();
        }

        private void OnPromptChanged(string prompt)
        {
            if (interactionPrompt != null) interactionPrompt.text = prompt ?? string.Empty;
        }

        private void OnGamePhaseChange(GamePhaseChangedEvent evt)
        {
            _isShopping = evt.NewPhase == GamePhase.Shopping;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            // 使用 CanvasGroup 隐藏，保留订阅以继续接收玩家通知。
            _canvasGroup.alpha = _hasInteractionTargets && !_isPlayingMiniGame && !_isShopping ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
