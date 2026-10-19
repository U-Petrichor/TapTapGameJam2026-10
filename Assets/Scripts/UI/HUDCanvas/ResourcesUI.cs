using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.Events;
using TapTapGameJam.Cockroach;

namespace TapTapGameJam.UI
{
    public class ResourcesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinsNumText;
        [SerializeField] private TMP_Text nutritionNumText;
        [SerializeField] private TMP_Text cockroachNumText;
        [SerializeField] private TMP_Text talentPointsNumText;
        [Header("蟑螂胜利进度")]
        [SerializeField] private CockroachManager cockroachManager;
        [SerializeField] private ArcLiquidProgress cockroachProgress;
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
            RefreshCockroachPopulation();

        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.Subscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.Subscribe<EventTalentPointChange>(OnTalentPointChange);
            EventPipeline.Subscribe<EventCockroachCountChange>(OnCockroachCountChange);
            RefreshCockroachPopulation();

        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCoinsChange>(OnCoinsChange);
            EventPipeline.UnSubscribe<EventNutritionChange>(OnNutritionChange);
            EventPipeline.UnSubscribe<EventTalentPointChange>(OnTalentPointChange);
            EventPipeline.UnSubscribe<EventCockroachCountChange>(OnCockroachCountChange);
        }
        private void RefreshCockroachPopulation()
        {
            if (cockroachManager == null) cockroachManager = CockroachManager.Instance;
            ShowCockroachPopulation(cockroachManager != null ? cockroachManager.CockroachTotalCount() : 0);
        }

        private void OnCockroachCountChange(EventCockroachCountChange evt)
        {
            ShowCockroachPopulation(evt.RemainingCockroach);
        }

        private void ShowCockroachPopulation(int count)
        {
            count = Mathf.Max(0, count);
            if (cockroachNumText != null) cockroachNumText.text = count.ToString();
            if (cockroachProgress != null && cockroachManager != null)
            {
                cockroachProgress.demoLoop = false;
                cockroachProgress.SetProgress(count / (float)cockroachManager.VictoryPopulation);
            }
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
