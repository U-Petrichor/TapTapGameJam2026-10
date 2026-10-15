using NUnit.Framework;
using TapTapGameJam.Items;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// "待拾取物"位：一个只装一件东西的小容器 + 槽位。
    ///
    /// - 作为容器（<see cref="IItemContainer"/>）：持有那一件，打开时给它建块、关掉时销毁它的块；
    /// - 作为槽位（<see cref="IItemSlot"/>）：可以往上丢（标记点空着就行），也可以把这件再拖走。
    ///
    /// 它和背包是独立容器：背包关掉时把它一起关掉即可，不需要谁去记录它的块。
    /// </summary>
    public class UnpickItemSlot : MonoBehaviour, IItemSlot, IItemContainer
    {
        [Tooltip("物品标记点：待拾取物停在这里。放在提示框中心")]
        [SerializeField] private RectTransform _itemSpritePivot;

        [Tooltip("物品提示 - 标题")]
        [SerializeField] private TextMeshProUGUI _infoTextTitle;

        [Tooltip("物品提示 - 内容")]
        [SerializeField] private TextMeshProUGUI _infoTextContent;

        /// <summary>当前停在这里的那件（容器持有的唯一物品）</summary>
        private PickableItem _item;

        public PickableItem Item => _item;

        #region IItemSlot

        public bool TryDropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "UnpickItemSlot：block 是 null");
            return _item == null;
        }

        public bool DropItemBlock(BagItemBlock block)
        {
            Assert.IsTrue(block != null, "UnpickItemSlot：block 是 null");

            if (_item != null)
            {
                return false;
            }

            SetItemBlock(block);
            return true;
        }

        /// <summary>块被拿走了：标记点空出来（块本身和 CurrentSlot 都不归这里管）</summary>
        public void OnPickUpItem(BagItemBlock block)
        {
            Assert.IsTrue(_item == block.boundedItem,
                $"UnpickItemSlot：拿走的不是我这里的那件（我是 {_item}，来的是 {block.boundedItem}）");

            _item = null;
            _infoTextTitle.text = "";
            _infoTextContent.text = "";
        }

        /// <summary>TODO: 待拾取位还没有试放预览的表现</summary>
        public void ClearDropPreview()
        {
        }

        #endregion

        #region IItemContainer

        /// <summary>换一件停在标记点上（比如装备槽换下来的那件）</summary>
        public void OpenWithItem(PickableItem item)
        {
            Assert.IsTrue(item != null, "UnpickItemSlot.OpenWithItem：item 是 null");

            if (_item != null && _item != item)
            {
                // 原来那件的块先收掉，不然它会留在拖动层上
                UIManager.Instance.DestroyBlock(_item);
                _item = null;
            }

            _item = item;
            OpenContainer();
        }

        public void OpenContainer()
        {
            if (_item == null)
            {
                // TODO: 这里该把这个提示框收起来（换一种显隐方式，别直接 SetActive 整块面板）
                return;
            }

            BagItemBlock block = UIManager.Instance.CreateBlock(_item);
            SetItemBlock(block);
        }

        public void CloseContainer()
        {
            if (_item == null)
            {
                return;
            }

            UIManager.Instance.DestroyBlock(_item);

            _item = null;
            _infoTextTitle.text = "";
            _infoTextContent.text = "";

            // TODO: 同上，把提示框收起来
        }

        #endregion

        /// <summary>把块停到标记点上，并显示提示</summary>
        private void SetItemBlock(BagItemBlock block)
        {
            _item = block.boundedItem;
            _infoTextContent.text = _item.HoverPrompt;
            _infoTextTitle.text = _item.name;

            // 从仓库过来的块要先恢复成背包形态（显示格子框、缩放归 1）
            block.ApplyBagPresentation();
            block.CurrentSlot = this;
            block.transform.SetParent(UIManager.Instance.DragLayer, false);
            block.transform.position = _itemSpritePivot.position;
        }
    }
}
