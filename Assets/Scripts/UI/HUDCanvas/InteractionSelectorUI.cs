using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.UI
{
    public class InteractionSelectorUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractionManager interactionManager; // 获取玩家是为了得到内部维护的"可交互对象列表"
        [SerializeField] private Transform contents; // 获取存放文本内容的父对象
        [SerializeField] private InteractionObjectUI interactionObjectPrefab;
        private Dictionary<IInteractable, InteractionObjectUI> _uiDictionary = new();
        private InteractionObjectUI prevSelectedUI = null;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogError($"[InteractionSelectorUI]:没有配置CanvasGroup");
            }
        }
        private void Start()
        {
            if (interactionManager == null)
            {
                Debug.LogError("InteractionSelector: Interaction manager reference is not set.");
            }
            if (contents == null)
            {
                Debug.LogError("InteractionSelector: Contents reference is not set.");
            }
            if (interactionObjectPrefab == null)
            {
                Debug.LogError("InteractionSelector: Interaction object prefab reference is not set.");
            }
        }
        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.EventObjectAdded += OnObjectAdded;
                interactionManager.EventObjectDeleted += OnObjectDeleted;
                interactionManager.EventTargetChanged += OnTargetChanged;
            }
            EventPipeline.Subscribe<EventPlayMiniGame>(OnPlayMiniGame);
            EventPipeline.Subscribe<GamePhaseChangedEvent>(OnGamePhaseChange);
        }

        private void OnDisable()
        {
            interactionManager.EventObjectAdded -= OnObjectAdded;
            interactionManager.EventObjectDeleted -= OnObjectDeleted;
            interactionManager.EventTargetChanged -= OnTargetChanged;
            EventPipeline.UnSubscribe<EventPlayMiniGame>(OnPlayMiniGame);
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(OnGamePhaseChange);
        }
        private void OnObjectAdded(IInteractable interactable)
        {
            // 直接实例化UI对象，并将其添加到字典中
            InteractionObjectUI ui = Instantiate(interactionObjectPrefab, contents);
            ui.RefreshInteractionPrompt(interactable.InteractionPrompt);
            _uiDictionary.Add(interactable, ui);
        }
        private void OnObjectDeleted(IInteractable interactable)
        {
            if (_uiDictionary.TryGetValue(interactable, out InteractionObjectUI ui))
            {
                Destroy(ui.gameObject);
                _uiDictionary.Remove(interactable);
            }
        }
        private void OnTargetChanged(IInteractable interactable)
        {
            if (interactable == null)
            {
                // 传入的对象为空，使用在最后一个对象被删除时
                return;
            }
            if (prevSelectedUI != null)
            {
                prevSelectedUI.OnDeselected();
            }
            _uiDictionary.TryGetValue(interactable, out InteractionObjectUI ui);
            ui.OnSelected();
            prevSelectedUI = ui;
        }
        private void OnPlayMiniGame(EventPlayMiniGame evt)
        {
            if (evt.IsStart == true)
            {
                // 小游戏刚开始，这个UI要隐藏
                _canvasGroup.alpha = 0;
            }
            else
            {
                // 小游戏结束的时候，这个UI要呈现出来
                _canvasGroup.alpha = 1;
            }
        }
        private void OnGamePhaseChange(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase == GamePhase.Shopping)
            {
                // 进入商店时这个UI要隐藏
                _canvasGroup.alpha = 0;
            }
        }
    }
}