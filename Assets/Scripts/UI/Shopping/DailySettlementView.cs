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

        public void Show(int coinsEarned, int talentEarned, int giftedCoins, int giftedTalent)
        {
            Confirmed = false;
            _coinsText.text = $"赚取金币：{coinsEarned}+{giftedCoins}（赠送）";
            _talentText.text = $"获得天赋点：{talentEarned}+{giftedTalent}（赠送）";
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
