using TapTapGameJam.Audio;
using UnityEngine;
using TapTapGameJam.Events;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class ESCMenu : MonoBehaviour
    {
        public static ESCMenu Instance { get; private set; }
        [SerializeField] private Button ContinueGameBtn;
        [SerializeField] private Button RestartTheGameBtn;
        [SerializeField] private Button SettingsBtn;
        [SerializeField] private Button ReturnToMainMenuBtn;
        [SerializeField] private Button ExitGameBtn;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }
            Instance = this;
            BindEvents();
            CloseESCMenu();
        }
        private void BindEvents()
        {
            // 在Insepctor中这个按钮还绑定了另一个方法，那个方法负责让玩家切回
            if (ContinueGameBtn != null) ContinueGameBtn.onClick.AddListener(() => EventPipeline.Publish<ESCMenuEvent>(new ESCMenuEvent(false)));
            // if (RestartTheGameBtn != null) RestartTheGameBtn.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            if (SettingsBtn != null) SettingsBtn.onClick.AddListener(OpenSettingsMenu);
            if (ReturnToMainMenuBtn != null) ReturnToMainMenuBtn.onClick.AddListener(ReturnToMainMenu);
            if (ExitGameBtn != null) ExitGameBtn.onClick.AddListener(ExitGame);
        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<ESCMenuEvent>(HandleESCMenuEvent);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<ESCMenuEvent>(HandleESCMenuEvent);
        }
        private void OnDestroy()
        {
            if (ContinueGameBtn != null) ContinueGameBtn.onClick.RemoveAllListeners();
            if (RestartTheGameBtn != null) RestartTheGameBtn.onClick.RemoveAllListeners();
            if (SettingsBtn != null) SettingsBtn.onClick.RemoveAllListeners();
            if (ReturnToMainMenuBtn != null) ReturnToMainMenuBtn.onClick.RemoveAllListeners();
            if (ExitGameBtn != null) ExitGameBtn.onClick.RemoveAllListeners();
        }
        private void HandleESCMenuEvent(ESCMenuEvent evt)
        {
            if (evt.IsOpen)
            {
                OpenESCMenu();
            }
            else
            {
                CloseESCMenu();
            }
        }
        public void OpenESCMenu()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(true);
            }
        }
        public void CloseESCMenu()
        {
            SettingsMenu.CloseGameSettings();
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        private void RestartTheGame()
        {
            // 还没设计怎么重开游戏
            // UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        private void OpenSettingsMenu()
        {
            SettingsMenu.OpenInGame(() =>
            {
                if (SettingsBtn != null && UnityEngine.EventSystems.EventSystem.current != null)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(SettingsBtn.gameObject);
            });
        }
        private void ReturnToMainMenu()
        {
            // 这里可以添加返回主菜单的逻辑，比如加载主菜单场景
            Sfx.Play(SoundId.Click, this);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        private void ExitGame()
        {
            // 这里应该还有个弹窗询问玩家是否直接退出游戏，否则玩家误触损失很大
            // 但是还没搞
            Application.Quit();
        }
    }

}