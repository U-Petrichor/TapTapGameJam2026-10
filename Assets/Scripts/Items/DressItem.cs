using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    public class DressItem : PickableItem
    {
        public override bool UseInBag(PlayerBasic player)
        {
            if (player == null) return false;

            // TODO: 打开装备页面，进行装备。装备成功后由背包把本物品的 Location 设为 Equipped。
            // 装备栏未实现前，这里返回 false，不消耗物品。
            return false;
        }
    }
}
