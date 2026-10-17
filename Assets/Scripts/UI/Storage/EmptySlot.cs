using NUnit.Framework;
using TapTapGameJam.Items;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 物品底板：最下层那个"兜底落点"。
    ///
    /// - 它是 <see cref="IItemSlot"/>：物品丢在它上面时，**只有背包单独开着**才停进待拾取位
    ///   （<see cref="UIManager.IsBagContainerOpenOnly"/>），其它情况一律"丢回世界"。
    ///   注意物品在按下拖动的那一刻就已经被 <c>DetachItemToWorld</c> 送回世界了，
    ///   所以这里其实只需要决定"要不要再把它停到待拾取位"。
    /// - 它**不再自己挂**那些容器引用：谁开谁关由 <see cref="UIManager"/> 统一判断。
    /// - 不开 UI 时 <see cref="UIManager.RefreshActionMap"/> 会调 <see cref="SetEnabled"/> 把它关掉 ——
    ///   它平时贴在屏幕最下层，不关掉就会一直挡着鼠标点击。
    /// </summary>
    [DisallowMultipleComponent]
    public class EmptySlot : MonoBehaviour, IItemSlot
    {
        /// <summary>真正挡射线的是它身上那块 Image；光关组件挡不住</summary>
        private Image _raycastImage;

        /// <summary>
        /// 整体开关（由 UIManager 在开 / 关 UI 时同步）：
        /// 组件停用 + 那块 Image 的射线关掉。Image 懒取，免得依赖 Awake 顺序。
        /// </summary>
        public void SetEnabled(bool value)
        {
            enabled = value;

            _raycastImage ??= GetComponent<Image>();
            if (_raycastImage != null)
            {
                _raycastImage.raycastTarget = value;
            }
        }

        #region IItemSlot

        /// <summary>底板什么都接得住（丢在哪都算"落在这块板上了"）</summary>
        public bool TryDropItemBlock(BagItemBlock block)
        {
            return true;
        }

        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "EmptySlot：block 是 null");

            UIManager ui = UIManager.Instance;

            if (ui.IsBagContainerOpenOnly)
            {
                // 只有背包开着：把它停进待拾取位。
                // 一件物品只能有一个块、而建块是容器的活，所以先销毁手上这块，再走待拾取位自己的建块路径
                PickableItem item = block.boundedItem;
                ui.DestroyBlock(block);
                ui.UnpickItemContainer.OpenWithItem(item);
                return true;
            }

            // 其它情况都是丢回世界：本体早就回世界了，这里只要销毁块
            ui.DestroyBlock(block);
            return true;
        }

        /// <summary>块被拿走了：待拾取位腾空并关掉（块本身和 CurrentSlot 都不归这里管）</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            UnpickItemContainer unpick = UIManager.Instance.UnpickItemContainer;

            Assert.IsTrue(unpick.Item != null && unpick.Item == block.boundedItem,
                "EmptySlot：拿走的不是我这里的那件");

            unpick.SetItemBlock(null);
            unpick.CloseContainer();
            return true;
        }

        /// <summary>底板不做试放预览</summary>
        public void ClearDropPreview()
        {
        }

        #endregion
    }
}
