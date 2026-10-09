using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TapTapGameJam.Items;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 装备槽（头 / 身 / 足）。点击时：
    /// - 手上有能穿进这个槽的饰品 → 穿上去（槽里原来的回到手上）
    /// - 手是空的且有已装备的饰品 → 脱下来拿在手上
    /// - 其余情况什么都不做（摆放规则在 PlayerBag 里）
    /// </summary>
    [DisallowMultipleComponent]
    public class BagSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public EquipSlot Slot { get; private set; }

        private Image _background;
        private Image _icon;
        private TMP_Text _label;

        private Color _baseColor = Color.white;
        private bool _isHovered;
        private bool _isEmpty = true;
        private PickableItem _shownItem;

        public void Initialize(EquipSlot slot, Image background, Image icon, TMP_Text label, Color baseColor)
        {
            Slot = slot;
            _background = background;
            _icon = icon;
            _label = label;
            _baseColor = baseColor;

            if (_label != null)
            {
                _label.text = SlotName(slot);
            }

            Show(null);
        }

        /// <summary>显示槽里的饰品，null 表示空槽。内容没变就不重复刷（界面每帧都会调用它）</summary>
        public void Show(PickableItem item)
        {
            if (_shownItem == item)
            {
                return;
            }

            _shownItem = item;
            _isEmpty = item == null;

            if (_icon != null)
            {
                _icon.enabled = !_isEmpty;
                if (!_isEmpty)
                {
                    _icon.sprite = BagPanelUI.ResolveIcon(item);
                    // 图标可能为空（物品没配 bagIcon）：用接近白色的色块兜底，至少能看出"这里有一件"
                    _icon.color = _icon.sprite != null ? Color.white : new Color(0.95f, 0.85f, 0.45f, 1f);
                }
            }

            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            Apply();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            BagPanelUI.NotifySlotClicked(this, eventData.button);
        }

        private void Apply()
        {
            if (_background == null)
            {
                return;
            }

            Color color = _isEmpty ? _baseColor : Color.Lerp(_baseColor, Color.white, 0.15f);
            _background.color = _isHovered ? Color.Lerp(color, Color.white, 0.3f) : color;
        }

        private static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Body: return "身";
                case EquipSlot.Legs: return "足";
                case EquipSlot.Head:
                default: return "头";
            }
        }
    }
}
