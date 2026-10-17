using UnityEngine;

namespace TapTapGameJam.UI
{
    public class FloorSlot: MonoBehaviour, IItemSlot
    {
        // 不可放置物品的槽位，主要用于给底板加槽位，用来接住Drop（底板外Drop物品才丢弃，底板上Drop是正常返回）
        
        public bool TryDropItemBlock(BagItemBlock block)
        {
            return false;
        }

        /// <summary>把一个block放入这个槽位</summary>
        public bool DropItemBlock(BagItemBlock block)
        {
            return false;
        }

        /// <summary>底板本身不装东西，也不会成为谁的来源槽位；真要问就一律放行</summary>
        public bool OnPickUpItem(BagItemBlock block)
        {
            return true;
        }

        /// <summary>把这个槽位刷出来的试放预览收掉</summary>
        public void ClearDropPreview()
        {
            ;
        }
        
    }
}