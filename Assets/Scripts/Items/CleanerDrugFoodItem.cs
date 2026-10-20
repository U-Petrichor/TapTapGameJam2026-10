using TapTapGameJam.Cockroach;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
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
        public override string InteractionPrompt => "杀虫饵";

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            // 交互即清除
            Destroy(gameObject);
        }
    }
}
