using UnityEngine.Assertions;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 商店货架上的一个格子：一格一件商品。块由容器摆进格子（<see cref="StorePanelUI.LayoutBlockToCell"/>）。
    ///
    /// 【状态与表现】只看两件事：有没有货、买没买下。
    /// - 有货且**未**买下 → 显示价格文字；
    /// - 有货且**已**买下 → 底色变绿，价格文字收起；
    /// - 空货位 / 已售罄 → 底色回预制体原本的颜色，不显示价格。
    /// 高亮（选中）本身**不在这里体现**：选中是容器的事，表现是"那一格的块放大、恢复成背包形态"。
    ///
    /// 【交互】
    /// - **点击 = 选中**（<see cref="OnPointerClick"/> → <see cref="StorePanelUI.OnClickCell"/>，全商店唯一、互斥）；
    /// - **购买只走容器接口**（<see cref="StorePanelUI.TryBuyHighlighted"/>），点格子自己不买；
    /// - 拖拽不参与高亮和购买：没买下的货拖不动，买下的货直接拖出去；
    /// - 往空格子上放东西 = 归还（见 <see cref="DropItemBlock"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    public class StoreCellUI : MonoBehaviour, IItemSlot, ICellItemClick, IPointerClickHandler
    {
        #region 序列化引用（在格子预制体上连）

        [Header("显示")]
        [Tooltip("价格文字：有货且没买下时显示")]
        [SerializeField] private TextMeshProUGUI _priceText;

        [Tooltip("有货且已买下时，格子底色变成这个颜色")]
        [SerializeField] private Color _purchasedColor = new Color(0.35f, 0.85f, 0.45f, 1f);

        #endregion

        /// <summary>这一格属于哪一页（Food / Tool / Dress）</summary>
        public ItemCategory Page { get; private set; }

        /// <summary>在**本页**里的格子下标</summary>
        public int Index { get; private set; }

        /// <summary>是不是当前选中的那一格（全商店唯一）。表现由容器负责，这里只存状态</summary>
        public bool IsHighlighted { get; private set; }

        /// <summary>
        /// 这一格现在摆的是哪件货。**每次实时向后端要** —— 商店随机刷新会换掉整批货，
        /// 缓存引用会指到被销毁的旧对象上。
        /// </summary>
        public MouseStore.Goods Goods =>
            storePanelUI != null ? storePanelUI.GoodsAt(Page, Index) : null;

        private Image _background;

        /// <summary>预制体上原本的底色（"其他情况"要回到它）</summary>
        private Color _originalColor = Color.white;

        public StorePanelUI storePanelUI;

        public void Initialize(ItemCategory page, int index)
        {
            Assert.IsTrue(index >= 0, $"StoreCellUI：格子下标 {index} 非法");
            Assert.IsTrue(_priceText != null, "StoreCellUI：格子预制体上的价格文字没连");

            _background = GetComponent<Image>();
            Assert.IsTrue(_background != null,
                "StoreCellUI：格子根节点上没有 Image —— 已购买的绿色是改它的底色");

            Page = page;
            Index = index;
            IsHighlighted = false;
            _originalColor = _background.color;

            // 空格子上没有块可点，得靠底色自己接住点击（点它 = 选中）
            _background.raycastTarget = true;

            Refresh();
        }

        #region IItemSlot

        /// <summary>预览：空货位 + 类别对得上才收（用于把东西**还回**商店）</summary>
        public bool TryDropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            return storePanelUI.TryDropToCell(block, this);
        }

        /// <summary>真放：登记进货架、标记为已购买，并把块按商店样式摆好</summary>
        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            return storePanelUI.DropToCell(block, this);
        }

        /// <summary>拿取：没买下的货不许拖，买下的直接放行（判定在容器里，不会替玩家买单）</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            return storePanelUI.OnItemPickedUp(this, block);
        }

        /// <summary>商店不做试放预览</summary>
        public void ClearDropPreview()
        {
        }

        #endregion

        #region ICellItemClick / 点击

        /// <summary>点这一格 = 选中它（唯一、互斥），购买交给按钮</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnClickCell(this);
        }

        /// <summary>块压在格子上时，点击走 <see cref="BagItemBlock.OnPointerClick"/> → 这里，也是选中</summary>
        public void OnClickItem(BagItemBlock block, PointerEventData.InputButton button)
        {
            if (button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnClickCell(this);
        }

        #endregion

        #region 选中状态 / 显示

        /// <summary>选中状态（由容器唯一持有）。高亮的表现不在这里 —— 容器会去处理那一格的块</summary>
        public void SetHighlighted(bool highlighted)
        {
            IsHighlighted = highlighted;
        }

        /// <summary>
        /// 按"有没有货 / 买没买下"刷一遍显示：
        /// 价格只在有货且未买下时出现；底色只有"有货且已买下"才变绿，其余回预制体原色。
        /// </summary>
        public void Refresh()
        {
            MouseStore.Goods goods = Goods;
            bool hasItem = goods != null && goods.item != null;
            bool purchased = hasItem && goods.purchased;

            if (_priceText != null)
            {
                bool showPrice = hasItem && !purchased;
                _priceText.gameObject.SetActive(showPrice);

                if (showPrice)
                {
                    _priceText.text = "$" + goods.price;
                }
            }

            if (_background != null)
            {
                _background.color = purchased ? _purchasedColor : _originalColor;
            }
        }

        #endregion
    }
}
