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

        /// <summary>从某个槽位中拿起物品时</summary>
        public void OnPickUpItem(BagItemBlock block)
        {
            ;
        }

        /// <summary>把这个槽位刷出来的试放预览收掉</summary>
        public void ClearDropPreview()
        {
            ;
        }
        
    }
}