using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 地图上的食物。剩余营养可以被蟑螂啃掉，也可以被玩家收进背包。
    ///
    /// 【谁能吃 / 怎么吃】蟑螂只认三件事：
    /// <list type="bullet">
    /// <item><see cref="EatPosition"/>：从它当前位置"够得着"的那个点；</item>
    /// <item><see cref="EatRadius"/>：够得着的半径；</item>
    /// <item><see cref="Consume"/>：真正啃掉多少。</item>
    /// </list>
    /// 默认实现就是「自己脚下 + <see cref="interactRaius"/>」，
    /// 体积很大的食物（<see cref="WallFoodItem"/>：一面墙、一张桌子）把前两个覆写成
    /// 「碰撞盒上离蟑螂最近的点」，否则蟑螂永远够不到 pivot 在墙角的那块墙。
    /// </summary>
    public class FoodItem : PickableItem
    {
        [Tooltip("总营养")] public float totalNutrition = 10;

        [Tooltip("剩余营养")] public float remainingNutrition;

        [Tooltip("可交互半径")] public float interactRaius = 0.5f;

        /// <summary>仓库分页：食物归 Food 页</summary>
        public override ItemCategory Category => ItemCategory.Food;

        
        /// <summary>还有没有吃的价值，没有的话 ItemManager 不应该再返回它</summary>
        public bool HasNutrition => remainingNutrition > 0f;

        #region 蟑螂取食接口

        /// <summary>
        /// 蟑螂从 <paramref name="from"/>（世界 XZ）来吃时，实际对着的那个点。
        ///
        /// 默认就是自己脚下；墙体 / 桌子这种大体型食物覆写成"包围盒上离它最近的点"，
        /// 所以蟑螂沿着墙的任意位置都能啃，而不是必须挤到 pivot 附近。
        /// </summary>
        public virtual Vector2 EatPosition(Vector2 from)
        {
            Vector3 p = transform.position;
            return new Vector2(p.x, p.z);
        }

        /// <summary>
        /// 够得着的半径。蟑螂的判定是 <c>InteractRadius + EatRadius</c>。
        /// 默认等于 <see cref="interactRaius"/>。
        /// </summary>
        public virtual float EatRadius => interactRaius;

        /// <summary>
        /// 天亮清场（<see cref="NightManager.CleanupNightLeftovers"/>）要不要把它一起清掉。
        ///
        /// 地上的食物、蟑螂尸体都只属于夜晚，所以要清；但被
        /// <see cref="WallFoodItem"/> 注入过的墙 / 桌子是**场景本体**，
        /// 清掉就是把地图拆了，必须返回 false。
        /// </summary>
        public virtual bool IsClearedAtDay => true;

        #endregion

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

        /// <summary>营养归零后的收尾只跑一次（销毁是帧末生效的，同一帧里可能被多次查询）</summary>
        private bool _depletedHandled;

        /// <summary>
        /// 被蟑螂啃掉 amount 份营养，返回实际吃到的量（0 表示没吃到）。
        /// </summary>
        public float Consume(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            if (!HasNutrition)
            {
                // 兜底：营养已经是 0 却还活着。
                //
                // 正常情况下走不到这里 —— 下面啃到 0 的那一帧就已经安排好销毁了。
                // 但这道保险很重要：一旦真的出现"营养 0 却躺在地上"的食物，
                // 它会变成永远啃不动、永远不消失的僵尸（每次查询都会占着列表），
                // 而且从外面完全看不出原因。补一次销毁，让它自己恢复。
                HandleDepleted();
                return 0f;
            }

            float eaten = Mathf.Min(amount, remainingNutrition);
            remainingNutrition -= eaten;

            // 【顺序很重要】先把"已经没了"这件事办完，再通知视觉。
            //
            // Destroy 是帧末执行的，所以下面的 OnConsumed 里照样能读到还活着的组件、
            // 也照样能画出最后一帧（100% 裂纹），但"销毁"这件事已经安排上了。
            // 反过来写（先 OnConsumed 再 Destroy）会留一个很难查的坑：
            // 子类的视觉代码一旦抛异常，销毁就被跳过，食物卡成僵尸。
            if (!HasNutrition)
            {
                HandleDepleted();
            }

            OnConsumed(eaten);

            return eaten;
        }

        /// <summary>
        /// 营养归零时的统一收尾：注销 → 子类收尾 → 销毁。幂等，重复调用只会执行一次。
        /// </summary>
        private void HandleDepleted()
        {
            if (_depletedHandled)
            {
                return;
            }

            _depletedHandled = true;

            // 立刻注销，别等到本帧末尾的 OnDisable：
            // 否则同一帧里下一只蟑螂查询时，还会看到这块已经被吃空的食物
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.Unregister(this);
            }

            try
            {
                // 消失前的收尾。必须在 Destroy 之前调：销毁之后组件就取不到自己的 bounds 了
                OnDepleted();
            }
            finally
            {
                // 放在 finally 里：子类收尾就算抛异常，也绝不能让它变成「营养 0 却永远不消失」的僵尸
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 兜底巡检：营养已经归零、却还没被销毁的话，补一次销毁。
        ///
        /// 正常路径不需要它 —— <see cref="Consume"/> 啃到 0 的那一帧就已经安排好销毁了。
        /// 留这个入口是给外部巡检用的：万一有别的代码直接写了 <see cref="remainingNutrition"/>、
        /// 或者销毁被什么东西打断，也不至于留下一块永远啃不动、永远不消失的食物。
        /// </summary>
        public void DestroyIfDepleted()
        {
            if (!HasNutrition)
            {
                HandleDepleted();
            }
        }

        /// <summary>被啃掉一口之后调（包括这一口把最后一点啃完的情况）。默认什么都不做。</summary>
        protected virtual void OnConsumed(float eaten)
        {
        }

        /// <summary>
        /// 营养归零、即将被销毁时的收尾。默认什么都不做。
        /// 此时 <see cref="ItemManager"/> 已经注销了自己，但组件本身还活着（Destroy 帧末才生效）。
        /// </summary>
        protected virtual void OnDepleted()
        {
        }

        public override string InteractionPrompt => "捡起食物";
    }
}
