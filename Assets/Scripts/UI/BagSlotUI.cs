using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TapTapGameJam.Items;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 装备槽（头 / 身 / 足）。挂在场景里那三个现成的 Image 上。
    ///
    /// 交互：
    /// - 把 BagItemBlock 拖到槽上 → 由 BagPanelUI 负责穿上去（槽里原有的那件送回世界）
    /// - 右键 → 脱下来，变成"待拾取的物品"
    ///
    /// 这个类只提供"我代表哪个槽"和指针回调，穿脱规则都在 BagPanelUI / PlayerBag 里。
    /// </summary>
    [DisallowMultipleComponent]
    public class BagSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public EquipSlot Slot { get; private set; }

        private Image _background;
        private Color _baseColor = Color.white;
        private bool _isHovered;

        /// <summary>TODO: 槽里的饰品图标怎么显示还没定；定了之后在这里按 BagPanelUI.ResolveIcon 换图</summary>
        private PickableItem _shownItem;

        public BagPanelUI bagPanelUI;
        public PickableItem ShownItem => _shownItem;

        public void Initialize(EquipSlot slot, Image background)
        {
            Slot = slot;
            _background = background;
            _baseColor = background != null ? background.color : Color.white;
            Apply();
        }

        /// <summary>记录槽里显示的饰品（目前只影响底色）</summary>
        public void SetShownItem(PickableItem item)
        {
            if (_shownItem == item)
            {
                return;
            }

            _shownItem = item;
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
            bagPanelUI.NotifySlotClicked(this, eventData.button);
        }

        private void Apply()
        {
            if (_background == null)
            {
                return;
            }

            Color color = _shownItem != null ? Color.Lerp(_baseColor, Color.white, 0.15f) : _baseColor;
            _background.color = _isHovered ? Color.Lerp(color, Color.white, 0.3f) : color;
        }
    }
}
