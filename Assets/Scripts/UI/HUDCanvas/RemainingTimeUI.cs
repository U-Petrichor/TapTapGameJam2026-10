using UnityEngine;
using TapTapGameJam.Core;

namespace TapTapGameJam.UI
{
    public class RemainingTimeUI : MonoBehaviour
    {
        [field: SerializeField] public TMPro.TextMeshProUGUI RemainingTimeText { get; private set; }
        [field: SerializeField] private GamePhaseManager _gamePhaseManager;

        private void Update()
        {
            int seconds = Mathf.CeilToInt(_gamePhaseManager.RemainingTime);
            RemainingTimeText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }

}