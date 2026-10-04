using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items.tools
{
    public class HealthDrug : PickableItem
    {
        // 药品案例：使用时回复玩家一定生命值

        [Tooltip("回复生命值量")] 
        [SerializeField]
        private float recoverHealth;

        public override bool UseInBag(PlayerBasic player)
        {
            if (player == null)
            {
                return false;
            }

            // TODO: 玩家身上目前只有 Coins / Nutrition（PlayerResources），还没有生命值组件。
            // 等你确定生命值挂在哪个脚本上之后，这里改成 player.XXX.RecoverHealth(recoverHealth)，
            // 然后 return true 表示消耗掉这瓶药。
            return false;
        }
    }
}
