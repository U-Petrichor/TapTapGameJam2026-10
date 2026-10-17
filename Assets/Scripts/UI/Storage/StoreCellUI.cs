using NUnit.Framework;
using TapTapGameJam.Items;
using TapTapGameJam.Player;
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
    /// - **不允许放入**：<see cref="TryDropItemBlock"/> / <see cref="DropItemBlock"/> 一律 false
    ///   （这轮也不做"卖给商店"）；
    /// - **拿取要过两道关**：这一格有货 + 这件货已经买下。没买下就当场拒绝，并把这一格记成高亮，
    ///   交给 <see cref="StorePanelUI.OnBuyButton"/> 去买（购买按钮买的就是高亮那一件）。
    /// </summary>
    [DisallowMultipleComponent]
    public class StoreCellUI : MonoBehaviour, IItemSlot, ICellItemClick
    {
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.3f, 1f);

        /// <summary>这一格属于哪一页（Food / Tool / Dress）</summary>
        public ItemCategory Page { get; private set; }

        /// <summary>在**本页**里的格子下标</summary>
        public int Index { get; private set; }

        /// <summary>这一格摆的是哪件商品（可能已经售罄：<c>Goods.item == null</c>）</summary>
        public MouseStore.Goods Goods { get; private set; }

        private Image _background;
        private Color _baseColor = Color.white;
        private bool _isHighlighted;

        public StorePanelUI storePanelUI;

        public void Initialize(ItemCategory page, int index, MouseStore.Goods goods,
            Image background, Color baseColor)
        {
            Assert.IsTrue(background != null, "StoreCellUI：底色层没建出来");
            Assert.IsTrue(index >= 0, $"StoreCellUI：格子下标 {index} 非法");

            Page = page;
            Index = index;
            Goods = goods;
            _background = background;
            _baseColor = baseColor;
            _isHighlighted = false;
            Apply();
        }

        #region IItemSlot（商店不接受放下）

        /// <summary>商店不接受放下（这轮也不做卖东西）</summary>
        public bool TryDropItemBlock(BagItemBlock block)
        {
            return false;
        }

        /// <summary>商店不接受放下</summary>
        public bool DropItemBlock(BagItemBlock block)
        {
            return false;
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

        #region ICellItemClick

        /// <summary>点一下商品格 = 选中它（购买按钮买选中的那件）</summary>
        public void OnClickItem(BagItemBlock block, PointerEventData.InputButton button)
        {
            Assert.IsTrue(storePanelUI != null, "StoreCellUI：没连 storePanelUI");
            storePanelUI.OnClickCell(this);
        }

        #endregion

        #region 高亮

        /// <summary>选中的那格会被点亮（由容器唯一持有）</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted)
            {
                return;
            }

            _isHighlighted = highlighted;
            Apply();
        }

        private void Apply()
        {
            if (_background == null)
            {
                return;
            }

            _background.color = _isHighlighted ? Color.Lerp(_baseColor, HighlightColor, 0.55f) : _baseColor;
        }

        #endregion
    }
}
