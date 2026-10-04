using TapTapGameJam.Core;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 地图上的食物。剩余营养可以被蟑螂啃掉，也可以被玩家收进背包。
    /// </summary>
    public class FoodItem : PickableItem
    {
        [Tooltip("总营养")] public float totalNutrition;

        [Tooltip("剩余营养")] public float remainingNutrition;

        /// <summary>还有没有吃的价值，没有的话 ItemManager 不应该再返回它</summary>
        public bool HasNutrition => remainingNutrition > 0f;

        protected override void OnEnable()
        {
            remainingNutrition = totalNutrition;
            base.OnEnable();
        }

        public override bool CanInteract(PlayerBasic player)
        {
            // 被吃空的食物不该再能被捡起来
            return base.CanInteract(player) && HasNutrition;
        }

        public float Consume(float amount)
        {
            if (amount <= 0f || !HasNutrition)
            {
                return 0f;
            }

            float eaten = Mathf.Min(amount, remainingNutrition);
            remainingNutrition -= eaten;

            if (!HasNutrition)
            {
                // 吃干净了：先立刻从 ItemManager 注销，别等到本帧末尾的 OnDisable。
                // 否则同一帧里下一只蟑螂查询时，还会看到这块已经被吃空的食物。
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.Unregister(this);
                }

                Destroy(gameObject);
            }

            return eaten;
        }

        public override bool UseInBag(PlayerBasic player)
        {
            if (player == null)
            {
                return false;
            }

            // TODO: 在player所在地创建该FoodItem实例（保留剩余营养），也就是掉落回地图
            // 大致流程：ItemManager.TryDropToMap(this, player.transform.position)
            return false;
        }

        public override string HoverPrompt => $"剩余营养 {remainingNutrition:F1}";
        public override string InteractionPrompt => "捡起食物";
    }
}
