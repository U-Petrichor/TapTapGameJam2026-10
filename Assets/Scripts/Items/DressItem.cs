using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 饰品：可以穿在头 / 身 / 足三个槽位上。
    /// 穿戴与换装的规则全部由 PlayerBag 负责，这里只负责回答"我属于哪个槽"和"我想穿"。
    /// </summary>
    public class DressItem : PickableItem
    {
        public enum DressType
        {
            DressHead,
            DressBody,
            DressLegs
        }

        [Tooltip("装饰类别（头甲足）")]
        public DressType dressType;

        /// <summary>仓库分页：饰品归 Dress 页</summary>
        public override ItemCategory Category => ItemCategory.Dress;

        /// <summary>本饰品对应的装备槽</summary>
        public EquipSlot ToSlot()
        {
            switch (dressType)
            {
                case DressType.DressBody:
                    return EquipSlot.Body;
                case DressType.DressLegs:
                    return EquipSlot.Legs;
                case DressType.DressHead:
                default:
                    return EquipSlot.Head;
            }
        }
    }
}
