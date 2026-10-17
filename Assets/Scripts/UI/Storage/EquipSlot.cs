using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TapTapGameJam.Items;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 装备槽（头 / 身 / 足）。挂在场景里那三个现成的 Image 上。
    ///
    /// 它是个 <see cref="IItemSlot"/>，但穿脱规则都在容器 <see cref="BagPanelUI"/> / 后端
    /// <see cref="PlayerBag"/> 里，这里只负责"我代表哪个槽"和把拖放转发过去。
    /// 部位在 Inspector 里选（"是哪个槽"是场景信息，不再由代码按顺序 Initialize）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IItemSlot
    {
        [Tooltip("这个槽是哪个部位。三个槽分别选 头 / 身 / 足")]
        [SerializeField] private Items.EquipSlot _slot;

        public Items.EquipSlot Slot => _slot;

        /// <summary>容器。在 Inspector 里从 BagPanel 上拖过来</summary>
        public BagPanelUI bagPanelUI;

        private Image _background;
        private Color _baseColor = Color.white;
        private bool _isHovered;

        /// <summary>TODO: 槽里的饰品图标怎么显示还没定；定了之后在这里换图并调 Apply()</summary>
        private PickableItem _shownItem;

        public PickableItem ShownItem => _shownItem;

        private void Awake()
        {
            _background = GetComponent<Image>();
            Assert.IsTrue(_background != null, $"{name}：EquipSlot 要挂在有 Image 的物体上");
            _baseColor = _background.color;
        }

        public void SetShownItem(PickableItem item)
        {
            _shownItem = item;
            Apply();
        }

        #region IItemSlot（判定与穿脱都在容器里）

        /// <summary>预览：装备槽永远"放得下"（可以替换），但类型和部位必须对 —— 由容器判定</summary>
        public bool TryDropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(bagPanelUI != null, "EquipSlot：没连 bagPanelUI");
            return bagPanelUI.TryEquipToSlot(this, block, true);
        }

        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(bagPanelUI != null, "EquipSlot：没连 bagPanelUI");
            return bagPanelUI.TryEquipToSlot(this, block, false);
        }

        /// <summary>从槽里把东西拿起来：先从后端卸下来（本体落到世界），块自己会带着来源回退</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(bagPanelUI != null, "EquipSlot：没连 bagPanelUI");
            return bagPanelUI.OnItemPickedUp(block);
        }

        /// <summary>TODO: 装备槽的试放预览还没有表现（要红/绿得先定槽里怎么显示图标）</summary>
        public void ClearDropPreview()
        {
        }

        #endregion

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
