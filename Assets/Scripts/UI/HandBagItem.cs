using TapTapGameJam.Items;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>手持物品是从哪儿来的</summary>
    public enum HandItemOrigin
    {
        /// <summary>从地图上捡起来、还没进过背包（就是 PlayerBag 的 PendingPickupItem）</summary>
        Unpicked,

        /// <summary>从背包格子里拖出来的。放不下时要回到 <see cref="HandBagItem.FromOrigin"/></summary>
        FromBag,

        /// <summary>从装备槽里拖出来的。放不下时要穿回 <see cref="HandBagItem.FromSlot"/></summary>
        FromSlot,
        
        /// <summary>从商店里拖出来的。放不下时要穿回 <see cref="HandBagItem.FromSlot"/></summary>
        FromStore,
        
        /// <summary>从仓库里拖出来的。放不下时要穿回 <see cref="HandBagItem.FromSlot"/></summary>
        FromRepo,
    }

    /// <summary>
    /// 界面上"当前正被拿着/拖着的那件物品"。
    ///
    /// 这是纯粹的界面状态，放在 PlayerBag 里没有意义：
    /// - 后端只知道"这件东西属不属于玩家、占不占格子"；
    /// - 而"现在鼠标正拖着它、它原来是从哪个格子/哪个槽拿出来的"只有界面知道。
    /// </summary>
    public class HandBagItem
    {
        /// <summary>手上这件物品</summary>
        public PickableItem Item { get; set; }

        /// <summary>它对应的表现块（拖动中就是鼠标上那一块）</summary>
        public BagItemBlock Block { get; set; }

        /// <summary>从哪来的</summary>
        public HandItemOrigin Origin { get; set; }

        /// <summary>Origin == FromBag / Store / Repo 时：它原来放在哪个格子（放不下就回这里）</summary>
        public Vector2Int FromOrigin { get; set; }

        /// <summary>Origin == FromSlot 时：它原来穿在哪个槽（放不下就穿回去）</summary>
        public EquipSlot FromSlot { get; set; }

        public bool IsValid => Item != null && Block != null;

        public void Clear()
        {
            Item = null;
            Block = null;
            Origin = HandItemOrigin.Unpicked;
            FromOrigin = default;
            FromSlot = default;
        }
    }
}
