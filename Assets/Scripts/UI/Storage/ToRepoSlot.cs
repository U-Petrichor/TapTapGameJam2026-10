using TapTapGameJam.Items;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.UI
{
    public class ToRepoSlot: MonoBehaviour, IItemSlot
    {
        [SerializeField] private PlayerRepo _repo;

        public bool TryDropItemBlock(BagItemBlock block)
        {
            transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            return true;
        }

        /// <summary>把一个block放入这个槽位</summary>
        public bool DropItemBlock(BagItemBlock block)
        {
            ItemCategory page;
            int index;
            if (_repo.PutItem(block.boundedItem, out page, out index))
            {
                UIManager.Instance.DestroyBlock(block);
                return true;   
            }

            return false;
        }

        /// <summary>从这个槽位拿起物品。返回 false = 不允许取出</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            return false;
        }

        /// <summary>把这个槽位刷出来的试放预览收掉</summary>
        public void ClearDropPreview()
        {
            transform.localScale = Vector3.one;
        }
        

    }
}