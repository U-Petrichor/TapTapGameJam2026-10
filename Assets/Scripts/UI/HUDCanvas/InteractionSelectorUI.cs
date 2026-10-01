using System.Collections.Generic;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
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
        }

        private void OnDisable()
        {
            interactionManager.EventObjectAdded -= OnObjectAdded;
            interactionManager.EventObjectDeleted -= OnObjectDeleted;
            interactionManager.EventTargetChanged -= OnTargetChanged;
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
    }
}