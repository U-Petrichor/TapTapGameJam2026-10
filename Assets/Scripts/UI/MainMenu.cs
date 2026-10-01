using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TapTapGameJam.UI
{
    public class MainMenuUIInteract : MonoBehaviour
    {
        [Header("Buttons")]
        // 将所有需要交互的按钮在这里声明
        [SerializeField] private Button startGameBtn;
        [SerializeField] private Button settingsBtn;
        [SerializeField] private Button exitBtn;

        private void Awake()
        {
            // 在代码中集中进行事件绑定
            BindEvents();
        }

        private void BindEvents()
        {
            if(startGameBtn != null) startGameBtn.onClick.AddListener(() => SceneManager.LoadScene("GameScene"));
            // if (settingsBtn != null) settingsBtn.onClick.AddListener(() => SettingsManager.Instance.OpenESCMenu());

            if (exitBtn != null) exitBtn.onClick.AddListener(() => {
                Debug.Log("执行退出游戏命令！");
                Application.Quit();
            });
        }

        private void OnDestroy()
        {
            if (startGameBtn != null) startGameBtn.onClick.RemoveAllListeners();
            if (settingsBtn != null) settingsBtn.onClick.RemoveAllListeners();
            if (exitBtn != null) exitBtn.onClick.RemoveAllListeners();
        }
    }
}