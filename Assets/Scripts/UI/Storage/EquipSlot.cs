using UnityEngine.Assertions;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 装备槽（头 / 身 / 足）。挂在场景里那三个现成的 Image 上。
    ///
    /// 【表现】槽里**不画 BagItemBlock**：块进"代理模式"（CanvasGroup alpha 0，但射线照旧），
    /// 装备物品的图直接贴在槽自己的 Image 上（只换 sprite，不动尺寸，自动 preserveAspect）；
    /// 试放预览也是同一套做法，收预览 / 换目标时恢复。
    ///
    /// 【交互】块虽然看不见，但**射线还是它挡着** —— 所以按在槽上其实就是在按那块，
    /// 拖拽事件天然落到块自己身上（槽不用接拖拽，也没有自己的点击行为）。
    /// 槽只负责两件容器要它做的事：往槽里放（<see cref="TryDropItemBlock"/> / <see cref="DropItemBlock"/>）
    /// 和"物品被拿走了"的通知（<see cref="OnPickUpItem"/>，用来清图 + 让块退出代理模式）。
    /// </summary>
    [DisallowMultipleComponent]
    public class EquipSlot : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IItemSlot
    {
        [Tooltip("这个槽是哪个部位。三个槽分别选 头 / 身 / 足")]
        [SerializeField] private Items.EquipSlot _slot;

        public Items.EquipSlot Slot => _slot;

        /// <summary>容器。在 Inspector 里从 BagPanel 上拖过来</summary>
        public BagPanelUI bagPanelUI;

        private Image _background;
        private Color _baseColor = Color.white;

        /// <summary>场景里原本的图 / 保比例开关：空槽时要回到它</summary>
        private Sprite _baseSprite;
        private bool _basePreserveAspect;

        private bool _isHovered;

        /// <summary>槽里正式装备的那件（空槽是 null）</summary>
        private PickableItem _shownItem;

        /// <summary>正在试放预览的那件（null = 没在预览）</summary>
        private PickableItem _previewItem;

        public PickableItem ShownItem => _shownItem;

        private void Awake()
        {
            _background = GetComponent<Image>();
            Assert.IsTrue(_background != null, $"{name}：EquipSlot 要挂在有 Image 的物体上");

            _baseColor = _background.color;
            _baseSprite = _background.sprite;
            _basePreserveAspect = _background.preserveAspect;
        }

        /// <summary>换"槽里正式装备的那件"（卸下 / 换装时由容器调，传 null = 空槽）</summary>
        public void SetShownItem(PickableItem item)
        {
            _shownItem = item;
            Apply();
        }

        /// <summary>试放预览：把这一件的图贴到槽上；收预览时回落到正式装备那件</summary>
        public void SetDropPreview(PickableItem item)
        {
            _previewItem = item;
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

        /// <summary>物品离开这个槽：先从后端卸下来（本体落到世界、由容器安排去处），槽上的图跟着清掉</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(bagPanelUI != null, "EquipSlot：没连 bagPanelUI");
            Assert.IsTrue(block != null, "EquipSlot.OnPickUpItem：block 是 null");

            bool taken = bagPanelUI.OnItemPickedUp(block);
            if (taken)
            {
                // 块要跟着鼠标走了：退出代理模式，恢复背包那套表现（可见、格子框、原大小）
                block.ApplyBagPresentation();
                SetShownItem(null);
            }

            return taken;
        }

        /// <summary>收掉试放预览：回落到槽里那一件的图（空槽回原本的样子）</summary>
        public void ClearDropPreview()
        {
            if (_previewItem == null)
            {
                return;
            }

            _previewItem = null;
            Apply();
        }

        #endregion

        #region 表现

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

        /// <summary>
        /// 把要显示的那件（预览优先，其次正式装备）的图贴到本槽 Image 上：
        /// 只换 sprite、不动 rect（尺寸不变），并打开 preserveAspect 保比例；
        /// 有图时颜色压成白色，免得槽位底色把物品图染一层。
        /// 空槽 / 那件没配图 → 回到场景里原本的 sprite / 保比例开关 / hover 底色。
        /// </summary>
        private void Apply()
        {
            if (_background == null)
            {
                return;
            }

            PickableItem shown = _previewItem ?? _shownItem;

            if (shown != null && shown.BagIcon != null)
            {
                _background.sprite = shown.BagIcon;
                _background.preserveAspect = true;
                _background.color = Color.white;
                return;
            }

            _background.sprite = _baseSprite;
            _background.preserveAspect = _basePreserveAspect;
            _background.color = _isHovered ? Color.Lerp(_baseColor, Color.white, 0.3f) : _baseColor;
        }

        #endregion
    }
}
