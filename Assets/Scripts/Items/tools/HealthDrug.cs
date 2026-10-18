using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items.tools
{
    public class HealthDrug : ToolItem
    {
        // 药品案例：使用时回复玩家一定生命值 / 体力 / 解毒

        [Tooltip("回复生命值量")]
        [SerializeField] private int recoverDamage = 0;
        
        [Tooltip("回复体力量")]
        [SerializeField] private int recoverPower = 0;

        [Tooltip("回复中毒量")]
        [SerializeField] private int recoverPoison = 0;
        

        public override bool UseItem(PlayerBasic player)
        {
            if (player == null)
            {
                return false;
            }

            PlayerHealth playerHealth = player.PlayerHealth;
            if (playerHealth.HasDamaged && recoverDamage > 0
                || playerHealth.HasPoison && recoverPoison > 0
                || !playerHealth.IsFullPower && recoverPower > 0)
            {
                playerHealth.ReduceDamage(recoverDamage);
                playerHealth.ReducePoison(recoverPoison);
                playerHealth.AddPower(recoverPower);
                return true;
            }
            return false;
        }
    }
}
