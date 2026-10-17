using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items.tools
{
    public class HealthDrug : ToolItem
    {
        // 药品案例：使用时回复玩家一定生命值

        [Tooltip("回复生命值量")]
        [SerializeField]
        private float recoverHealth;

        public override bool UseItem(PlayerBasic player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.PlayerHealth.HasDamaged)
            {
                player.PlayerHealth.ReduceDamage(recoverHealth);
                return true;
            }
            return false;
        }
    }
}
