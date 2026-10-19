using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class TaskInteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionManager interactionManager;
        [SerializeField] private Image taskPromptImage;
        [SerializeField] private GameObject interactionKeyHint;
        private CanvasGroup _canvasGroup;
        private bool _notInDayTime;

        private void Awake() => _canvasGroup = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (interactionManager != null)
                interactionManager.EventTargetChanged += OnTargetChanged;
            EventPipeline.Subscribe<GamePhaseChangedEvent>(OnGamePhaseChange);
            _notInDayTime = GameManager.HasInstance && !GameManager.IsDay;
            OnTargetChanged(interactionManager != null ? interactionManager.CurrentTarget : null);
        }

        private void OnDisable()
        {
            if (interactionManager != null)
                interactionManager.EventTargetChanged -= OnTargetChanged;
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnGamePhaseChange);
            OnTargetChanged(null);
        }

        private void OnTargetChanged(Interactable target)
        {
            if (taskPromptImage != null)
            {
                taskPromptImage.sprite = target != null ? target.InteractionPromptSprite : null;
                taskPromptImage.preserveAspect = true;
                taskPromptImage.raycastTarget = false;
            }
            RefreshVisibility();
        }

        private void OnGamePhaseChange(GamePhaseChangedEvent evt)
        {
            _notInDayTime = evt.NewPhase != GamePhase.Day;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool visible = !_notInDayTime && taskPromptImage != null && taskPromptImage.sprite != null;
            if (taskPromptImage != null) taskPromptImage.gameObject.SetActive(visible);
            if (interactionKeyHint != null) interactionKeyHint.SetActive(visible);
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
