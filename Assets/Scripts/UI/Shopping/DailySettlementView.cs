using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class DailySettlementView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private TMP_Text _talentText;
        [SerializeField] private Button _continueButton;
        public bool Confirmed { get; private set; }

        private void OnEnable() => _continueButton.onClick.AddListener(Confirm);
        private void OnDisable() => _continueButton.onClick.RemoveListener(Confirm);

        public void Show(int coins, int talent, int giftedTalent)
        {
            Confirmed = false;
            _coinsText.text = $"赚取金币：{coins}";
            _talentText.text = $"获得天赋点：{talent}+{giftedTalent}（赠送）";
            _continueButton.interactable = true;
            gameObject.SetActive(true);
        }

        private void Confirm()
        {
            Confirmed = true;
            _continueButton.interactable = false;
        }
    }
}
