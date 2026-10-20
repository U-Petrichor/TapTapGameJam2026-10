using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Items
{
    /// <summary>
    /// 地图上的食物。剩余营养可以被蟑螂啃掉，也可以被玩家收进背包。
    ///
    /// 【怎么被找到】食物不再靠"全局名单 + 逐个遍历"，而是登记进 <see cref="FoodManager"/>
    /// 的格子索引：每件食物占它所在的那一格（大体型的 <see cref="WallFoodItem"/> 覆写
    /// <see cref="GetOccupiedCells"/> 占一整片）。蟑螂只查自己周围几个格子。
    ///
    /// 【登记时机】<c>OnEnable</c> 早于 <c>Start</c>，那时候 <see cref="remainingNutrition"/> 还是 0，
    /// 提前登记会让蟑螂查到一件"没营养"的食物（而 <see cref="Consume"/> 有僵尸自愈逻辑，会当场把它删掉）。
    /// 所以登记统一走 <see cref="RegisterToFoodManager"/>，只在"初始化完成"之后调用；
    /// 注销则挂在 <c>OnDisable</c> —— 进背包 / 商店 / 仓库时物品都会被 <c>SetActive(false)</c>，
    /// 正好等价于"不在场上"。
    /// </summary>
    public class FoodItem : PickableItem
    {
        [Tooltip("总营养")] public float totalNutrition = 10;

        [Tooltip("剩余营养")] public float remainingNutrition;

        /// <summary>仓库分页：食物归 Food 页</summary>
        public override ItemCategory Category => ItemCategory.Food;

        /// <summary>还有没有吃的价值，没有的话 FoodManager 不应该再返回它</summary>
        public bool HasNutrition => remainingNutrition > 0f;

        /// <summary>营养已经被初始化过（<see cref="Start"/> 或 <see cref="WallFoodItem.Configure"/> 跑过了）</summary>
        public bool NutritionInitialized { get; protected set; }

        /// <summary>当前是否登记在 <see cref="FoodManager"/> 里</summary>
        private bool _foodRegistered;

        /// <summary>找不到 FoodManager 的警告只打一次</summary>
        private static bool _warnedNoManager;

        #region 空间索引

        /// <summary>
        /// 这件食物占用哪些格子（含端点，闭区间）。默认就是它脚下那一格。
        /// <see cref="WallFoodItem"/> 覆写成包围盒覆盖的一整片。
        /// </summary>
        public virtual void GetOccupiedCells(GridManager grid, out Vector2Int min, out Vector2Int max)
        {
            Vector3 p = transform.position;
            Vector2Int cell = grid.WorldToGrid(new Vector2(p.x, p.z));
            min = cell;
            max = cell;
        }

        /// <summary>登记进 FoodManager 的格子索引。幂等。</summary>
        protected void RegisterToFoodManager()
        {
            if (_foodRegistered)
            {
                return;
            }

            FoodManager manager = FoodManager.Instance;
            if (manager == null)
            {
                if (!_warnedNoManager)
                {
                    _warnedNoManager = true;
                    Debug.LogError(
                        $"[FoodItem:{name}]：场景里没有 FoodManager，食物不会被蟑螂找到。" +
                        "检查场景里那个挂着 FoodManager 的物体是否还在", this);
                }

                return;
            }

            if (manager.Register(this))
            {
                _foodRegistered = true;
            }
        }

        /// <summary>从格子索引里摘掉自己。幂等。</summary>
        protected void UnregisterFromFoodManager()
        {
            if (!_foodRegistered)
            {
                return;
            }

            _foodRegistered = false;

            FoodManager manager = FoodManager.Instance;
            // 场景卸载时 manager 可能已经先没了，判空
            if (manager != null)
            {
                manager.Unregister(this);
            }
        }

        #endregion

        #region 生命周期

        protected virtual void OnEnable()
        {
            // 重新启用（从背包丢回地上）→ 重新登记。
            // 首次启用时 NutritionInitialized 还是 false，交给 Start 去做
            if (NutritionInitialized)
            {
                RegisterToFoodManager();
            }
        }

        protected virtual void OnDisable()
        {
            // 进背包 / 商店 / 仓库都是 SetActive(false)，等价于"离开地图"
            UnregisterFromFoodManager();
        }

        public void RestoreNutrition(float value)
        {
            remainingNutrition = Mathf.Clamp(value, 0f, totalNutrition);
            NutritionInitialized = true;
        }

        protected virtual void Start()
        {
            if (!NutritionInitialized) remainingNutrition = totalNutrition;
            NutritionInitialized = true;
            RegisterToFoodManager();
        }

        #endregion

        /// <summary>
        /// 天亮清场（<see cref="NightManager.CleanupNightLeftovers"/>）要不要把它一起清掉。
        ///
        /// 地上的食物、蟑螂尸体都只属于夜晚，所以要清；但被 <see cref="WallFoodItem"/> 注入过的墙 / 桌子
        /// 是**场景本体**，清掉就是把地图拆了，必须返回 false。
        /// </summary>
        public virtual bool IsClearedAtDay => true;

        public override bool CanInteract(PlayerBasic player)
        {
            // 被吃空的食物不该再能被捡起来
            return base.CanInteract(player) && HasNutrition;
        }

        #region 被吃

        /// <summary>营养归零后的收尾只跑一次（销毁是帧末生效的，同一帧里可能被多次查询）</summary>
        private bool _depletedHandled;

        /// <summary>被蟑螂啃掉 amount 份营养，返回实际吃到的量（0 表示没吃到）。</summary>
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
                // 它会变成永远啃不动、永远不消失的僵尸，而且从外面完全看不出原因。
                HandleDepleted();
                return 0f;
            }

            float eaten = Mathf.Min(amount, remainingNutrition);
            remainingNutrition -= eaten;

            // 【顺序很重要】先把"已经没了"这件事办完，再通知视觉。
            //
            // Destroy 是帧末执行的，所以下面的 OnConsumed 里照样能读到还活着的组件、
            // 也照样能画出最后一帧，但"销毁"这件事已经安排上了。
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
        /// 兜底巡检：营养已经归零、却还没被销毁的话，补一次销毁。
        /// 正常路径不需要它，留给外部巡检用（见 <see cref="WallGnawingDirector"/>）。
        /// </summary>
        public void DestroyIfDepleted()
        {
            if (!HasNutrition)
            {
                HandleDepleted();
            }
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

            // 立刻从索引里摘掉，别等到帧末的 OnDisable：
            // 否则同一帧里下一只蟑螂查询时，还会看到这块已经被吃空的食物
            UnregisterFromFoodManager();

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

        /// <summary>被啃掉一口之后调（包括这一口把最后一点啃完的情况）。默认什么都不做。</summary>
        protected virtual void OnConsumed(float eaten)
        {
        }

        /// <summary>
        /// 营养归零、即将被销毁时的收尾。默认什么都不做。
        /// 此时已经注销出空间索引，但组件本身还活着（Destroy 帧末才生效）。
        /// </summary>
        protected virtual void OnDepleted()
        {
        }

        #endregion

        public override string InteractionPrompt => "捡起食物";
    }
}
