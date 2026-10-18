using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.Events;

namespace TapTapGameJam.UI
{
    public class ResourcesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinsNumText;
        [SerializeField] private TMP_Text nutritionNumText;
        [SerializeField] private TMP_Text cockroachNumText;
        [SerializeField] private TMP_Text talentPointsNumText;
        private CanvasGroup _canvasGroup;
        private void Awake()
        {
            if(coinsNumText == null)
            {
                Debug.LogError("[Resources]：没有配置CoinsNumText");
            }
            if(nutritionNumText == null)
            {
                Debug.LogError("[Resources]：没有配置NutritionNumText");
            }
            if (cockroachNumText == null)
            {
                Debug.LogError("[Cockroach]：没有配置CockroachNumText");
            }

            if (talentPointsNumText == null)
            {
                Debug.LogError("[Cockroach]：没有配置talentPointsNumText");
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
            coinsNumText.text = "0";
            nutritionNumText.text = "0";
            cockroachNumText.text = "0";

        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.Subscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.Subscribe<EventTalentPointChange>(OnTalentPointChange);

        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.UnSubscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.UnSubscribe<EventTalentPointChange>(OnTalentPointChange);
        }
        private void OnCoinsChange(EventCoinsChange evt)
        {
            coinsNumText.text = evt.RemainingCoins.ToString();
        }
        private void OnNutritionChange(EventNutritionChange evt)
        {
            nutritionNumText.text = evt.RemainingNutrition.ToString();
        }

        private void OnTalentPointChange(EventTalentPointChange evt)
        {
            talentPointsNumText.text = evt.RemainingTalentPoint.ToString();
        }


    }

}