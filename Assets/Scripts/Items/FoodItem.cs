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
        [Tooltip("总营养")] public float totalNutrition = 10;

        [Tooltip("剩余营养")] public float remainingNutrition;

        [Tooltip("可交互半径")] public float interactRaius = 0.5f;

        
        /// <summary>还有没有吃的价值，没有的话 ItemManager 不应该再返回它</summary>
        public bool HasNutrition => remainingNutrition > 0f;

        protected override void Start()
        {
            remainingNutrition = totalNutrition;
            base.Start();
        }

        public override bool CanInteract(PlayerBasic player)
        {
            // 被吃空的食物不该再能被捡起来
            return base.CanInteract(player) && HasNutrition;
        }

        /// <summary>
        /// 被蟑螂啃掉 amount 份营养，返回实际吃到的量（0 表示没吃到）。
        /// </summary>
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
            PlayerBag bag = player != null ? player.PlayerBag : null;
            if (bag == null)
            {
                Debug.LogWarning($"[{name}]：玩家身上没有 PlayerBag，无法把食物放回地图");
                return false;
            }

            if (!HasNutrition)
            {
                // 已经被吃空了，没必要再放回地图，直接让它消失
                return true;
            }

            // 食物不做"使用即吃掉"：从背包拿出来放回地图，营养值原样保留
            // （本体从没被销毁过，所以 remainingNutrition 不需要额外搬运）。
            // 先从网格摘掉再回地图，否则会留下指向同一件物品的旧占格记录。
            bag.TakeFromGrid(this);
            bag.ReturnToWorld(this, bag.EvaluateDropPosition());
            return false; // 东西还在，不消耗
        }

        public override string HoverPrompt => $"剩余营养 {remainingNutrition:F1}";
        public override string InteractionPrompt => "捡起食物";
    }
}
