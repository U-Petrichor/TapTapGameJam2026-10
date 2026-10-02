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
        }
        private void Start()
        {
            // 初始化显示
            CoinsNumText.text = "0";
            NutritionNumText.text = "0";

        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.Subscribe<EventNutritionChange>(OnNutritionChange);

        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.UnSubscribe<EventNutritionChange>(OnNutritionChange);
        }
        private void OnCoinsChange(EventCoinsChange evt)
        {
            CoinsNumText.text = evt.RemainingCoins.ToString();
        }
        private void OnNutritionChange(EventNutritionChange evt)
        {
            NutritionNumText.text = evt.RemainingNutrition.ToString();
        }
    }

}