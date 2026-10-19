using TapTapGameJam.Audio;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace TapTapGameJam.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class MainMenuUIInteract : MonoBehaviour
    {
        [SerializeField] private Button startGameBtn;
        [SerializeField] private Button continueGameBtn;
        [SerializeField] private Button settingsBtn;
        [SerializeField] private Button galleryBtn;
        [SerializeField] private Button exitBtn;
        [SerializeField] private GameObject settingsCanvas;
        [SerializeField] private GameObject galleryCanvas;
        [SerializeField] private GameObject exitConfirmation;
        [SerializeField] private Button settingsBackBtn;
        [SerializeField] private Button galleryBackBtn;
        [SerializeField] private Button cancelExitBtn;
        [SerializeField] private Button confirmExitBtn;
        [SerializeField] private IntroVideoTransition introTransition;
        private bool startingGame;
        private CanvasGroup menuGroup;
        private GameObject currentPanel;
        private Button returnButton;

        private void Awake()
        {
            SetMenuInteractable(true);
            startGameBtn.onClick.AddListener(StartGame);
            // Reserved until a save system is available.
            continueGameBtn.interactable = false;
            settingsBtn.onClick.AddListener(OpenSettings);
            galleryBtn.onClick.AddListener(OpenGallery);
            exitBtn.onClick.AddListener(OpenExit);
            settingsBackBtn.onClick.AddListener(ClosePanel);
            galleryBackBtn.onClick.AddListener(ClosePanel);
            cancelExitBtn.onClick.AddListener(ClosePanel);
            confirmExitBtn.onClick.AddListener(ConfirmExit);
            settingsCanvas.SetActive(false);
            galleryCanvas.SetActive(false);
            exitConfirmation.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && currentPanel != null)
                ClosePanel();
        }

        private void StartGame()
        {
            if (startingGame) return;
            Sfx.Play(SoundId.Click, this);
            if (introTransition == null)
            {
                Debug.LogError("开始游戏：请绑定 VideoCanvas 上的 IntroVideoTransition。", this);
                return;
            }
            if (!introTransition.Begin()) return;
            startingGame = true;
            SetMenuInteractable(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public void OpenSettings() => OpenPanel(settingsCanvas, settingsBtn, settingsBackBtn);
        public void OpenGallery() => OpenPanel(galleryCanvas, galleryBtn, galleryBackBtn);
        public void OpenExit() => OpenPanel(exitConfirmation, exitBtn, cancelExitBtn);

        private void OpenPanel(GameObject panel, Button source, Button first)
        {
            if (currentPanel != null) currentPanel.SetActive(false);
            SetMenuInteractable(false);
            currentPanel = panel;
            returnButton = source;
            panel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void ClosePanel()
        {
            if (currentPanel == null) return;
            currentPanel.SetActive(false);
            currentPanel = null;
            SetMenuInteractable(true);
            if (EventSystem.current != null && returnButton != null)
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        private void SetMenuInteractable(bool interactable)
        {
            // Unity objects must use Unity's null comparison, including after a script reload.
            if (menuGroup == null)
                menuGroup = GetComponent<CanvasGroup>();
            if (menuGroup == null)
                menuGroup = gameObject.AddComponent<CanvasGroup>();
            menuGroup.interactable = interactable;
        }

        public void ConfirmExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
