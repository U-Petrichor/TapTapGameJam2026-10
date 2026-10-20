using TapTapGameJam.Audio;
using TapTapGameJam.Core;
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
        [SerializeField] private Button exitBtn;
        [SerializeField] private GameObject settingsCanvas;
        [SerializeField] private GameObject exitConfirmation;
        [SerializeField] private Button settingsBackBtn;
        [SerializeField] private Button cancelExitBtn;
        [SerializeField] private Button confirmExitBtn;
        [SerializeField] private IntroVideoTransition introTransition;
        [Header("继续游戏可用状态（可选美术引用）")]
        [SerializeField] private CanvasGroup continueAvailabilityVisual;
        [SerializeField] private GameObject continueUnavailableHint;
        private bool startingGame;
        private CanvasGroup menuGroup;
        private GameObject currentPanel;
        private Button returnButton;

        private void Awake()
        {
            SetMenuInteractable(true);
            startGameBtn.onClick.AddListener(StartGame);
            SetContinueAvailable(SaveManager.TryRead(out _));
            continueGameBtn.onClick.AddListener(ContinueGame);
            settingsBtn.onClick.AddListener(OpenSettings);
            exitBtn.onClick.AddListener(OpenExit);
            settingsBackBtn.onClick.AddListener(ClosePanel);
            cancelExitBtn.onClick.AddListener(ClosePanel);
            confirmExitBtn.onClick.AddListener(ConfirmExit);
            settingsCanvas.SetActive(false);
            exitConfirmation.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && currentPanel != null)
                ClosePanel();
        }

        private void Start()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(startGameBtn.gameObject);
        }

        private void SetContinueAvailable(bool available)
        {
            continueGameBtn.interactable = available;
            if (continueAvailabilityVisual != null) continueAvailabilityVisual.alpha = 1f;
            if (continueGameBtn.targetGraphic != null)
                continueGameBtn.targetGraphic.color = available ? Color.white : new Color(.48f, .48f, .48f, 1f);
            var continueLabel = continueGameBtn.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (continueLabel != null)
                continueLabel.color = available ? Color.black : new Color(.3f, .3f, .3f, 1f);
            if (continueUnavailableHint != null) continueUnavailableHint.SetActive(!available);
            var startNavigation = startGameBtn.navigation;
            if (startNavigation.mode == Navigation.Mode.Explicit)
            {
                startNavigation.selectOnDown = available ? continueGameBtn : settingsBtn;
                startGameBtn.navigation = startNavigation;
                var settingsNavigation = settingsBtn.navigation;
                settingsNavigation.selectOnUp = available ? continueGameBtn : startGameBtn;
                settingsBtn.navigation = settingsNavigation;
            }
        }

        private void StartGame()
        {
            BeginGame(false);
        }

        private void ContinueGame() => BeginGame(true);

        private void BeginGame(bool continueSave)
        {
            if (startingGame) return;
            Sfx.Play(SoundId.Click, this);
            if (introTransition == null)
            {
                Debug.LogError("开始游戏：请绑定 VideoCanvas 上的 IntroVideoTransition。", this);
                return;
            }
            if (continueSave)
            {
                if (!SaveManager.PrepareContinue())
                {
                    SetContinueAvailable(false);
                    return;
                }
            }
            else SaveManager.PrepareNewGame();
            if (!introTransition.Begin(!continueSave && !PlayerProfile.HasWatchedIntro))
            {
                SaveManager.PrepareNewGame();
                return;
            }
            startingGame = true;
            SetMenuInteractable(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public void OpenSettings() => OpenPanel(settingsCanvas, settingsBtn, settingsBackBtn);
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
