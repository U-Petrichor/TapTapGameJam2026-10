using UnityEngine.EventSystems;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 只有"格子类"槽位才有的交互（右键使用物品之类）。
    /// 刻意不放进 <see cref="IItemSlot"/>：装备槽、待拾取位没有这套语义。
    /// </summary>
    public interface ICellItemClick
    {
        /// <summary>点在某格上。block 是那一格上物品的表现块，空格子传 null</summary>
        void OnClickItem(BagItemBlock block, PointerEventData.InputButton button);
    }
}
