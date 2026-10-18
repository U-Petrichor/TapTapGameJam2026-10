namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 一个 Buff 数值的作用方向。决定 <see cref="BuffAggregator.Apply"/> 怎么消费百分比：
    /// - <see cref="Grow"/>：越大越好，存正数表示「增加多少」（0.1 = +10%）
    /// - <see cref="Shrink"/>：越小越好，存正数表示「减免多少」（0.1 = 少受/少花 10%）
    /// - <see cref="Flat"/>：计数型（档位、容量），只吃 <see cref="BuffOp.Flat"/>，百分比无效
    /// - <see cref="Flag"/>：能力开关，只回答有没有，不参与任何数值运算
    /// </summary>
    public enum BuffKind
    {
        Grow,
        Shrink,
        Flat,
        Flag,
    }

    /// <summary>
    /// 全局 Buff 数值标识。<b>枚举顺序 = <see cref="BuffKeys"/> 里定义表的顺序</b>，两边必须同步增删。
    ///
    /// 命名约定：
    /// - 枚举名用可读的 PascalCase；
    /// - 存进资产 / 存档的稳定名字用 snake_case，见 <see cref="BuffKeys.Get"/> 的 Id，
    ///   与历史 <c>TalentEffectKey</c> 的字符串值逐一对齐，所以既有天赋资产不需要迁移。
    ///
    /// 谁来读：见 Docs/BuffSystem.md 的接入点表。标 [预留] 的目前没有任何消费方，
    /// 先占好位置，等对应玩法做出来再接线。
    /// </summary>
    public enum BuffKey
    {
        // ---------------- 玩家个体：移动 ----------------

        /// <summary>玩家移动速度加成。读：PlayerMovement.CurrentMoveSpeed。[对应天赋 疾足奔行]</summary>
        PlayerMoveSpeedPercent,

        /// <summary>[预留] 冲刺速度倍率的额外加成，叠在 PlayerMovement 的冲刺倍率上</summary>
        SprintSpeedPercent,

        // ---------------- 玩家个体：PlayerHealth ----------------

        /// <summary>体力上限比例增减。读：PlayerHealth.PowerTotal 的基准 MaxHealth。[预留]</summary>
        StaminaMaxPercent,

        /// <summary>体力上限固定值增减（与百分比同源，最终 =(base+flat)*factor）。[预留]</summary>
        StaminaMaxFlat,

        /// <summary>受伤减免。读：PlayerHealth.AddDamage 的入口。[对应天赋 坚韧甲壳/铁壁虫甲]</summary>
        PlayerDamageReduction,

        /// <summary>中毒影响减免。读：PlayerHealth.AddPoison 的入口。[对应天赋 抗毒体质]</summary>
        PoisonImpactReduction,

        /// <summary>体力（冲刺）损耗减免。读：PlayerHealth._sprintCostPerSecond。[对应天赋 节力爬行/持久耐劳]</summary>
        StaminaCostReduction,

        /// <summary>体力恢复速度加成。读：PlayerHealth._powerRecoverPerSecond。[对应天赋 蓄力回元]</summary>
        StaminaRecoveryPercent,

        /// <summary>解毒速度加成。读：中毒随时间自愈的速率（该玩法尚未实现）。[对应天赋 代谢排毒]</summary>
        DetoxSpeedPercent,

        /// <summary>背包负重减免。读：PlayerHealth.RefreshHeavyFromBag 的换算结果。[对应天赋 轻装上阵/减负疾行]</summary>
        BagWeightReduction,

        /// <summary>
        /// 背包扩容档位：0 = 未扩容，1 = 一级，2 = 二级（目前设计为三级）。
        /// 读：PlayerBag 按档位决定封锁哪些格子（网格固定更大，未解锁的格子不可放置）。
        /// [对应天赋 扩囊增容/巨囊载物] 注意：语义是「档位」，不是旧注释里的「额外格数」。
        /// </summary>
        BagCapacityTier,

        // ---------------- 虫群（所有 Cockroach 的群体增益） ----------------

        /// <summary>小蟑螂移速加成。读：Cockroach.UpdateLogic 里 Speed 的乘数。[对应天赋 群足疾行/虫行如风/虫潮奔涌]</summary>
        CockroachSpeedPercent,

        /// <summary>每只蟑螂单次取食量加成。读：Cockroach.TryEat 传给 FoodItem.Consume 的容量。[对应天赋 群颚快食/群虫饕食]</summary>
        CockroachCarryPercent,

        /// <summary>孵化（产卵）速度加成。读：CockNest.UpdateHatch 的每秒消耗速率。[对应天赋 群卵初繁/群卵盛繁/万卵齐生]</summary>
        CockroachHatchPercent,

        /// <summary>[预留] 母体（玩家个体）产卵速度加成，个体产卵玩法尚未实现。[对应天赋 母体催卵]</summary>
        PlayerEggProductionPercent,

        // ---------------- 巢穴 ----------------

        /// <summary>巢穴活动范围（探索半径）加成。读：CockNest.range 的乘数，改完要重生成范围圈。[对应天赋 纸巢拓土/巢域扩张/纸巢帝国]</summary>
        NestRangePercent,

        /// <summary>
        /// [预留] 巢穴容量固定值增减。目前巢穴容量没有上限，等做了上限再接线。
        /// </summary>
        NestCapacityFlat,

        // ---------------- 资源获取（PlayerResources） ----------------

        /// <summary>金币获取加成。读：PlayerResources.AddCoins。[对应天赋 勤工生财/日进斗金]</summary>
        CoinGainPercent,

        /// <summary>营养获取加成。[对应天赋 精细吸收/高效同化] 目前 PlayerResources.AddNutrition 还没有调用方。</summary>
        NutritionGainPercent,

        /// <summary>天赋点（怒气）获取加成。只作用于「获得」，撤销天赋的退款必须原额返还。[对应天赋 积怨成怒/怒火翻涌]</summary>
        TalentPointGainPercent,

        /// <summary>[预留] 咖啡获取加成。PlayerResources.CoffeeCount 已在用，倍率尚未接线。</summary>
        CoffeeGainPercent,

        // ---------------- 元属性 / 特殊 ----------------

        /// <summary>
        /// 装扮加成的额外提升。这是一条「放大别的 buff」的元属性：
        /// <see cref="BuffRules.AmplifyRules"/> 规定它只放大 <see cref="BuffSourceKind.Dress"/> 来的百分比，
        /// 且计算它自己的值时排除 Dress 来源（不会自乘）。[对应天赋 巧饰虫装/精工战衣]
        /// </summary>
        DressBonusPercent,

        /// <summary>
        /// 幸运值（固定值，加法累加：两个 +30% 等于 +60%）。
        /// 黑盒开出稀有物品的概率 = 基准 × (1 + 幸运)，公式在 <c>LootRoll.RareChance</c>。
        /// </summary>
        LuckFlat,

        // ---------------- 敌人 ----------------

        /// <summary>
        /// [预留] 守卫发现「玩家」的距离减免（不影响发现蟑螂的距离）。
        /// 目前决定先不做，key 先占位。接线时注意 VisionRange 同时烘焙了灯光与 MeshCollider，见 Docs/BuffSystem.md。
        /// </summary>
        GuardPlayerDetectReduction,

        // ---------------- 能力型 Tag（只问有没有） ----------------

        /// <summary>同类相食：小蟑螂死亡后变成少量食物。[对应天赋] Cockroach.Die 里尚未实现。</summary>
        FlagCockroachDeathFood,

        /// <summary>群颚破壁：小蟑螂可以啃噬墙体。墙体破坏玩法尚未实现。</summary>
        FlagGroupWallGnawing,

        /// <summary>瞬息落卵：个体瞬间产卵。个体产卵玩法尚未实现。</summary>
        FlagInstantEggLaying,

        /// <summary>绝境蜕生：抵抗一次致命伤。是否已经消耗掉由玩法自己记录，本系统只回答「有没有这个能力」。</summary>
        FlagLethalDamageProtection,

        /// <summary>蓄势突进：启用个体冲刺。注意冲刺目前默认就能用，语义待定。</summary>
        FlagIndividualSprint,

        /// <summary>破卵即生：个体瞬间孵化。个体孵化玩法尚未实现。</summary>
        FlagIndividualInstantHatching,
    }
}
