using NUnit.Framework;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 仓库里的一个格子：**一格一件**，判定和布局都在容器 <see cref="RepoPanelUI"/> 里，
    /// 这里只提供"我是哪一页的第几格"和把拖放转发过去。
    ///
    /// 【和背包的区别】
    /// - 每个格子放一个物品，而不是像背包一样一个物品占多个格子；
    /// - 仓库按 <see cref="ItemCategory"/> 分三页，这一格属于哪一页由 <see cref="Page"/> 决定，
    ///   放东西时物品类别必须和页对上（对不上的处理在 RepoPanelUI.TryDropToCell）；
    /// - BagItemBlock 放进仓库时要隐藏网格（CellFrame）、只显示 sprite，并按占格的 BoundingBox
    ///   缩放/居中（bbox 恰好塞满格子、中心在格心）—— 注意块的 pivot 是抓取格中心、
    ///   不是 bbox 中心，所以居中不能算坐标（见 RepoPanelUI.LayoutBlockToCell）；
    /// - 拿起时要先把块恢复成背包形态（显示格子框、缩放归 1），否则拖动过程中它还是"仓库样子"。
    /// </summary>
    [DisallowMultipleComponent]
    public class RepoCellUI : MonoBehaviour, IItemSlot, ICellItemClick, IPointerClickHandler
    {
        /// <summary>这一格属于哪一页（Food / Tool / Dress）</summary>
        public ItemCategory Page { get; private set; }

        /// <summary>在**本页**里的格子下标</summary>
        public int Index { get; private set; }

        private Image _background;
        private Image _preview;

        private Color _baseColor = Color.white;
        private Color _previewColor;
        private bool _hasPreview;

        public RepoPanelUI repoPanelUI;

        public void Initialize(ItemCategory page, int index, Image background, Image preview, Color baseColor)
        {
            Assert.IsTrue(background != null && preview != null, "RepoCellUI：底色 / 预览层没建出来");
            Assert.IsTrue(index >= 0, $"RepoCellUI：格子下标 {index} 非法");

            Page = page;
            Index = index;
            _background = background;
            _preview = preview;
            _baseColor = baseColor;
            _hasPreview = false;
            _previewColor = Color.clear;
            ApplyPreview();
        }

        #region IItemSlot（全部转发给容器）

        public bool TryDropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            return repoPanelUI.TryDropToCell(block, this);
        }

        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            return repoPanelUI.DropToCell(block, this);
        }

        public bool OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            return repoPanelUI.OnItemPickedUp(block);
        }

        /// <summary>预览是整个容器一起收的（换目标、松手都会来一下）</summary>
        public void ClearDropPreview()
        {
            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            repoPanelUI.HidePreview();
        }

        #endregion

        #region ICellItemClick / 点击

        /// <summary>
        /// 点这一格（或压在格子上的块，块会把点击转给所在槽位）：让容器把这一格物品的
        /// 标题 / 描述 / 图标刷到"显示信息"上。空货位就把信息清空。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            repoPanelUI.OnClickCell(this);
        }

        /// <summary>走 <see cref="ICellItemClick"/> 进来的点击（块转发）也当同一件事</summary>
        public void OnClickItem(BagItemBlock block, PointerEventData.InputButton button)
        {
            if (button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Assert.IsTrue(repoPanelUI != null, "RepoCellUI：没连 repoPanelUI");
            repoPanelUI.OnClickCell(this);
        }

        #endregion

        #region 预览表现

        /// <summary>刷这一格的试放预览（绿 = 能放，红 = 已经占了）</summary>
        public void SetDropPreview(Color color)
        {
            if (_hasPreview && _previewColor == color)
            {
                return;
            }

            _hasPreview = true;
            _previewColor = color;
            ApplyPreview();
        }

        public void ClearPreview()
        {
            if (!_hasPreview)
            {
                return;
            }

            _hasPreview = false;
            _previewColor = Color.clear;
            ApplyPreview();
        }

        /// <summary>缓存是唯一真相，Preview 只在这里被写（值没变就不碰 Image，避免弄脏 Canvas）</summary>
        private void ApplyPreview()
        {
            _preview.enabled = _hasPreview;
            _preview.color = _previewColor;
        }

        #endregion
    }
}
