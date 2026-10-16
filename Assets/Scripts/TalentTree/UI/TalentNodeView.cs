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
        [SerializeField] private Sprite _lockedSprite;
        [SerializeField] private Sprite _unlockedSprite;
        [SerializeField] private Image _selectedIndicator;
        [SerializeField] private Image _iconImage;

        private TalentTreeUI _owner;

        public string TalentId => _talentId;

        internal void Bind(TalentTreeUI owner)
        {
            _owner = owner;
            if (_button == null) _button = GetComponentInChildren<Button>(true);
            if (_titleText == null) _titleText = GetComponentInChildren<TMP_Text>(true);
            if (_stateImage == null) _stateImage = _button != null ? _button.targetGraphic as Image : GetComponent<Image>();
            if (_lockedSprite == null && _stateImage != null) _lockedSprite = _stateImage.sprite;
            SetSelected(false);
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _owner.SelectById(_talentId));
            }
        }

        internal void SetSelected(bool selected)
        {
            if (_selectedIndicator != null) _selectedIndicator.gameObject.SetActive(selected);
        }

        internal void Refresh(PlayerTalentTree playerTalentTree, TalentData data)
        {
            if (data == null || playerTalentTree == null) return;
            int level = playerTalentTree.GetLevel(data);
            if (_titleText != null) _titleText.text = data.DisplayName;
            if (_levelText != null) _levelText.text = level > 0 ? "已解锁" : "未解锁";
            if (_iconImage != null)
            {
                _iconImage.sprite = data.Icon;
                _iconImage.enabled = data.Icon != null;
            }
            if (_stateImage != null)
            {
                _stateImage.sprite = level > 0 ? _unlockedSprite : _lockedSprite;
                _stateImage.color = Color.white;
            }
        }
    }
}
