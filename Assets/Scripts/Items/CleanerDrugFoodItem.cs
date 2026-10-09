using TapTapGameJam.Cockroach;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 杀虫饵：由 <see cref="CleanerGuard"/> 放到地图上的 <see cref="FoodItem"/>。
    /// 蟑螂啃到就暴毙（击杀在 Cockroach.TryEat 里做），危险信号沿用蟑螂自己的死亡爆发。
    /// 没有额外配置项，也不做特效。
    /// </summary>
    public class CleanerDrugFoodItem : FoodItem
    {
        /// <summary>不给玩家捡：捡起来放回地图就成了玩家的杀蟑陷阱。</summary>
        public override bool CanInteract(Player.PlayerBasic player) => false;

        public override string HoverPrompt => "杀虫饵";

        public override string InteractionPrompt => "杀虫饵（不可拾取）";
    }
}
