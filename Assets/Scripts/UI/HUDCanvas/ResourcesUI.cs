using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.Events;

namespace TapTapGameJam.UI
{
    public class ResourcesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text CoinsNumText;
        [SerializeField] private TMP_Text NutritionNumText;
        [SerializeField] private TMP_Text CockroachNumText;

        private CanvasGroup _canvasGroup;
        private void Awake()
        {
            if(CoinsNumText == null)
            {
                Debug.LogError("[Resources]：没有配置CoinsNumText");
            }
            if(NutritionNumText == null)
            {
                Debug.LogError("[Resources]：没有配置NutritionNumText");
            }
            if (CockroachNumText == null)
            {
                Debug.LogError("[Cockroach]：没有配置CockroachNumText");
            }
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogError($"[ResourcesUI]:没有配置CanvasGroup");
            }
        }
        private void Start()
        {
            // 初始化显示
            CoinsNumText.text = "0";
            NutritionNumText.text = "0";
            CockroachNumText.text = "0";

        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.Subscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.Subscribe<EventPlayMiniGame>(OnPlayMiniGame); // 小游戏开始和结束时，这个UI要显示与隐藏

        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.UnSubscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.UnSubscribe<EventPlayMiniGame>(OnPlayMiniGame); // 小游戏开始和结束时，这个UI要显示与隐藏
        }
        private void OnCoinsChange(EventCoinsChange evt)
        {
            CoinsNumText.text = evt.RemainingCoins.ToString();
        }
        private void OnNutritionChange(EventNutritionChange evt)
        {
            NutritionNumText.text = evt.RemainingNutrition.ToString();
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

    }

}