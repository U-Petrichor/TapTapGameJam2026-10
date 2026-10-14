using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TalentTree
{
    /// <summary>
    /// 手工制作的天赋节点视图。把它挂在节点 UI 物体上，并填写对应的天赋 ID。
    /// 节点的 RectTransform、图片和文字位置完全由 UI 设计者控制。
    /// </summary>
    public sealed class TalentNodeView : MonoBehaviour
    {
        [SerializeField] private string _talentId;
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Image _stateImage;

        private TalentTreeUI _owner;

        public string TalentId => _talentId;

        internal void Bind(TalentTreeUI owner)
        {
            _owner = owner;
            if (_button == null) _button = GetComponent<Button>();
            if (_titleText == null) _titleText = GetComponentInChildren<TMP_Text>(true);
            if (_stateImage == null) _stateImage = GetComponent<Image>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _owner.SelectById(_talentId));
            }
        }

        internal void Refresh(PlayerTalentTree playerTalentTree, TalentData data)
        {
            if (data == null || playerTalentTree == null) return;
            int level = playerTalentTree.GetLevel(data);
            bool canUpgrade = playerTalentTree.CanUpgrade(data, out _);
            if (_titleText != null) _titleText.text = data.DisplayName;
            if (_levelText != null) _levelText.text = level > 0 ? "已解锁" : "未解锁";
            if (_stateImage != null)
                _stateImage.color = level > 0
                    ? new Color(0.25f, 0.55f, 0.3f, 1f)
                    : canUpgrade
                        ? new Color(0.65f, 0.55f, 0.2f, 1f)
                        : new Color(0.2f, 0.25f, 0.22f, 1f);
        }
    }
}
