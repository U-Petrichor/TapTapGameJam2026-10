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

        /// <summary>
        /// 右键使用 = 装备到对应槽位。
        /// 槽里已经有的那件会被 PlayerBag 换下来交到玩家手上，所以这里返回 false（东西没被消耗掉）。
        /// </summary>
        public override bool UseInBag(PlayerBasic player)
        {
            PlayerBag bag = player != null ? player.PlayerBag : null;
            if (bag == null)
            {
                Debug.LogWarning($"[{name}]：玩家身上没有 PlayerBag，无法装备");
                return false;
            }

            if (!bag.TryEquip(this, out _))
            {
                Debug.LogWarning($"[{name}]：装备失败");
            }

            return false;
        }
    }
}
