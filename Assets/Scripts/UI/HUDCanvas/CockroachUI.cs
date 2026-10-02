using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TapTapGameJam.Events;

namespace TapTapGameJam.UI
{
    public class CockroachUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text CockroachNumText;
        private void Awake()
        {
            if (CockroachNumText == null)
            {
                Debug.LogError("[Cockroach]：没有配置CockroachNumText");
            }
        }
        private void Start()
        {
            // 初始化显示
            CockroachNumText.text = "0";
        }
        private void OnEnable()
        {
            EventPipeline.Subscribe<EventCockroachCountChange>(OnCockroachCountChange);
        }
        private void OnDisable()
        {
            EventPipeline.UnSubscribe<EventCockroachCountChange>(OnCockroachCountChange);
        }
        private void OnCockroachCountChange(EventCockroachCountChange evt)
        {
            CockroachNumText.text = evt.RemainingCockroach.ToString();
        }
    }

}