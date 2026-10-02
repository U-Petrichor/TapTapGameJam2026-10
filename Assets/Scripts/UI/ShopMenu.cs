using UnityEngine;
using TapTapGameJam.Events;
using UnityEngine.UI;
using TapTapGameJam.Types;

namespace TapTapGameJam.UI
{
    public class ShopMenu : MonoBehaviour
    {
        [field:SerializeField]private Button _finishBtn;
        void Start()
        {
            CloseShopMenu();
        }
        private void OnEnable()
        {
            _finishBtn.onClick.AddListener(HandleFinishBtn);
        }
        private void OnDisable()
        {
            _finishBtn.onClick.RemoveListener(HandleFinishBtn);
        }
        public void OpenShopMenu()
        {
            foreach (Transform child in transform)
            {
                // 选择激活每一个子物体，否则关闭Canvas_Settings_Menu之后无法再次打开
                child.gameObject.SetActive(true);
            }
        }
        public void CloseShopMenu()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        public void HandleFinishBtn()
        {
            Debug.Log($"要发送事件拉");
            EventPipeline.Publish(new GamePhaseChangedEvent(GamePhase.Night));
        }
    }

}