using NUnit.Framework;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 商店货架上的一个格子：一格一件商品。显示逻辑和 <see cref="RepoCellUI"/> 一样
    /// （块进来时隐藏格子框、按 bbox 缩放居中，拿起来时由容器恢复背包形态）。
    ///
    /// 【和仓库格的区别】
    /// - **放下就是"归还"**：格子空着、类别对得上就收，物品照常进商店后端并被标成已购买
    ///   （归还的东西当然能再拿走）；
    /// - **悬停即高亮**：鼠标进这一格就亮、离开就灭（容器负责高亮、刷显示信息、把块放大回背包形态）；
    /// - **点一下就买**：<see cref="OnPointerClick"/> 直接尝试购买；拖起来时也是先尝试买、买成功才允许拿走；
    /// - 格子本身就是预制体：价格文字**常显**，购买提示**只在高亮时**显示（见 <see cref="Refresh"/>）。
    ///   格子的颜色完全由预制体决定，代码不碰。
    /// </summary>
    [DisallowMultipleComponent]
    public class StoreCellUI : MonoBehaviour,
        IItemSlot, ICellItemClick, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        #region 序列化引用（在格子预制体上连）

        [Header("显示")]
        [Tooltip("价格文字：一直显示（空货位空串，买下之后显示\"已购买\"）")]
        [SerializeField] private TextMeshProUGUI _priceText;

        [Tooltip("购买提示：只有这一格被选中（高亮）时才显示")]
        [SerializeField] private TextMeshProUGUI _buyHint;

        #endregion

        /// <summary>这一格属于哪一页（Food / Tool / Dress）</summary>
        public ItemCategory Page { get; private set; }

        /// <summary>在**本页**里的格子下标</summary>
        public int Index { get; private set; }

        /// <summary>
        /// 这一格现在摆的是哪件货。**每次实时向后端要** —— 商店随机刷新会换掉整批货，
        /// 缓存引用会指到被销毁的旧对象上。
        /// </summary>
        public MouseStore.Goods Goods =>
            storePanelUI != null ? storePanelUI.GoodsAt(Page, Index) : null;

        private bool _isHighlighted;

        public StorePanelUI storePanelUI;

        public void Initialize(ItemCategory page, int index)
        {
            Assert.IsTrue(_priceText != null, "StoreCellUI：格子预制体上的价格文字没连");
            Assert.IsTrue(_buyHint != null, "StoreCellUI：格子预制体上的购买提示没连");
            Assert.IsTrue(index >= 0, $"StoreCellUI：格子下标 {index} 非法");

            Page = page;
            Index = index;
            _isHighlighted = false;
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

        /// <summary>拿取：判定在容器里（有货 + 已买下才放行）</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            return storePanelUI.OnItemPickedUp(this, block);
        }

        /// <summary>商店不做试放预览（格子刷的是"高亮"，不是预览）</summary>
        public void ClearDropPreview()
        {
        }

        #endregion

        #region ICellItemClick / 点击

        /// <summary>
        /// 鼠标进入这一格 = 立刻高亮（不用点）：容器负责高亮、刷显示信息、把块恢复成背包形态。
        /// 块压在格子上时事件会沿层级冒泡到这里的 `GetEventHandler`，所以块也能触发。
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnCellHoverEnter(this);
        }

        /// <summary>鼠标离开 = 退出高亮、清掉显示信息、块缩回商店样式</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnCellHoverExit(this);
        }

        /// <summary>点这一格 = 直接尝试买下（买不了什么都不发生）</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnClickCell(this);
            Refresh();
        }

        /// <summary>点一下商品格 = 选中它；已经选中就是买下</summary>
        public void OnClickItem(BagItemBlock block, PointerEventData.InputButton button)
        {
            if (button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnClickCell(this);
            Refresh();
        }

        #endregion

        #region 高亮

        /// <summary>选中状态（由容器唯一持有）。表现只有一条：购买提示的显隐</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted)
            {
                return;
            }

            _isHighlighted = highlighted;
            Refresh();
        }

        /// <summary>价格常显，购买提示跟高亮走。**不碰预制体的颜色**</summary>
        public void Refresh()
        {
            MouseStore.Goods goods = Goods;
            if (goods == null)
            {
                _priceText.gameObject.SetActive(false);
                _buyHint.gameObject.SetActive(false);
            }
            else
            {
                if (_priceText != null)
                {
                    _priceText.text = "$" + goods.price.ToString();
                    _priceText.gameObject.SetActive(!goods.purchased);
                }

                if (_buyHint != null)
                {
                    _buyHint.gameObject.SetActive(_isHighlighted && !goods.purchased);
                }
            }
        }

        #endregion
    }
}
