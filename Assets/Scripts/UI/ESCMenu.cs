using TapTapGameJam.Audio;
using TapTapGameJam.Core;
using TapTapGameJam.Events;
using TapTapGameJam.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class ESCMenu : MonoBehaviour
    {
        public static ESCMenu Instance { get; private set; }
        [SerializeField] private Button ContinueGameBtn;
        [SerializeField] private Button SettingsBtn;
        [SerializeField] private Button GalleryBtn;
        [FormerlySerializedAs("ReturnToMainMenuBtn")]
        [SerializeField] private Button SaveAndExitBtn;
        [SerializeField] private Button ExitGameBtn;
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private GameObject galleryPanel;
        [SerializeField] private Button galleryBackBtn;
        [SerializeField] private GameObject exitConfirmation;
        [SerializeField] private Button cancelExitBtn;
        [SerializeField] private Button confirmExitBtn;
        [SerializeField] private TMP_Text saveStatus;
        private GameObject currentPanel;
        private Button returnButton;

        private void Awake()
        {
            Instance = this;
            if (ContinueGameBtn != null) ContinueGameBtn.onClick.AddListener(ResumeGame);
            if (SettingsBtn != null) SettingsBtn.onClick.AddListener(OpenSettings);
            if (GalleryBtn != null) GalleryBtn.onClick.AddListener(OpenGallery);
            if (SaveAndExitBtn != null) SaveAndExitBtn.onClick.AddListener(SaveAndExit);
            if (ExitGameBtn != null) ExitGameBtn.onClick.AddListener(OpenExit);
            if (galleryBackBtn != null) galleryBackBtn.onClick.AddListener(CloseSubPanel);
            if (cancelExitBtn != null) cancelExitBtn.onClick.AddListener(CloseSubPanel);
            if (confirmExitBtn != null) confirmExitBtn.onClick.AddListener(ConfirmExit);
            CloseESCMenu();
        }

        private void OnEnable() => EventPipeline.Subscribe<ESCMenuEvent>(HandleESCMenuEvent);
        private void OnDisable() => EventPipeline.UnSubscribe<ESCMenuEvent>(HandleESCMenuEvent);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ContinueGameBtn != null) ContinueGameBtn.onClick.RemoveListener(ResumeGame);
            if (SettingsBtn != null) SettingsBtn.onClick.RemoveListener(OpenSettings);
            if (GalleryBtn != null) GalleryBtn.onClick.RemoveListener(OpenGallery);
            if (SaveAndExitBtn != null) SaveAndExitBtn.onClick.RemoveListener(SaveAndExit);
            if (ExitGameBtn != null) ExitGameBtn.onClick.RemoveListener(OpenExit);
            if (galleryBackBtn != null) galleryBackBtn.onClick.RemoveListener(CloseSubPanel);
            if (cancelExitBtn != null) cancelExitBtn.onClick.RemoveListener(CloseSubPanel);
            if (confirmExitBtn != null) confirmExitBtn.onClick.RemoveListener(ConfirmExit);
        }

        private void HandleESCMenuEvent(ESCMenuEvent evt)
        {
            if (evt.IsOpen) OpenESCMenu();
            else CloseESCMenu();
        }

        public void OpenESCMenu()
        {
            // Legacy test scenes remain usable until explicitly rebuilt by the authoring tool.
            if (menuRoot == null)
            {
                foreach (Transform child in transform) child.gameObject.SetActive(true);
                return;
            }
            menuRoot.SetActive(true);
            if (galleryPanel != null) galleryPanel.SetActive(false);
            if (exitConfirmation != null) exitConfirmation.SetActive(false);
            currentPanel = null;
            SetMenuInteractable(true);
            if (saveStatus != null) saveStatus.text = "进度按阶段检查点保存，继续游戏将从本阶段开始。";
            Select(SettingsBtn);
        }

        public void CloseESCMenu()
        {
            SettingsMenu.CloseGameSettings();
            currentPanel = null;
            foreach (Transform child in transform) child.gameObject.SetActive(false);
        }

        private void ResumeGame()
        {
            Sfx.Play(SoundId.Click, this);
            var input = GameManager.Player != null ? GameManager.Player.InputManager : FindFirstObjectByType<PlayerInputManager>();
            if (input != null) input.SwitchESCMenu();
            else EventPipeline.Publish(new ESCMenuEvent(false));
        }

        private void OpenSettings()
        {
            SetMenuInteractable(false);
            SettingsMenu.OpenInGame(() => { SetMenuInteractable(true); Select(SettingsBtn); });
            if (!SettingsMenu.IsGameSettingsOpen) SetMenuInteractable(true);
        }

        private void OpenGallery() => OpenPanel(galleryPanel, GalleryBtn, galleryBackBtn);
        private void OpenExit() => OpenPanel(exitConfirmation, ExitGameBtn, cancelExitBtn);

        private void OpenPanel(GameObject panel, Button source, Button first)
        {
            if (panel == null) return;
            Sfx.Play(SoundId.Click, this);
            if (currentPanel != null) currentPanel.SetActive(false);
            currentPanel = panel;
            returnButton = source;
            SetMenuInteractable(false);
            panel.SetActive(true);
            Select(first);
        }

        // Called by the input action before toggling the pause menu itself.
        public bool TryCloseSubPanel()
        {
            if (SettingsMenu.CloseGameSettings()) return true;
            if (currentPanel == null) return false;
            CloseSubPanel();
            return true;
        }

        private void CloseSubPanel()
        {
            if (currentPanel == null) return;
            currentPanel.SetActive(false);
            currentPanel = null;
            SetMenuInteractable(true);
            Select(returnButton);
        }

        private void SetMenuInteractable(bool enabled)
        {
            if (menuGroup == null) return;
            menuGroup.interactable = enabled;
            menuGroup.blocksRaycasts = enabled;
        }

        private static void Select(Button button)
        {
            if (button != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void SaveAndExit()
        {
            Sfx.Play(SoundId.Click, this);
            if (!SaveManager.SaveLatestCheckpoint())
            {
                if (saveStatus != null) saveStatus.text = "保存失败，未退出游戏。请稍后重试。";
                return;
            }
            SetMenuInteractable(false);
            SceneManager.LoadScene("MainMenu");
        }

        private void ConfirmExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
