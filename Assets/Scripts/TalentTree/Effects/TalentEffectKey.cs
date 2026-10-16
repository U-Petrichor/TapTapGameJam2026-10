namespace TalentTree.Effects
{
    /// <summary>
    /// 天赋效果的稳定标识。同一效果的不同档位共用一个 Key。
    /// 百分比增益使用小数（0.1 表示 +10%），减免也使用正数（0.1 表示 -10%）；
    /// 容量使用额外格数，能力使用 0 / 1。能力的冷却、次数与消耗由对应玩法管理。
    /// </summary>
    public static class TalentEffectKey
    {
        // 资源增益
        public const string CoinGainMultiplier = "coin_gain_multiplier"; // 勤工生财、日进斗金：金币获取加成。
        public const string NutritionGainMultiplier = "nutrition_gain_multiplier"; // 精细吸收、高效同化：营养获取加成。
        public const string TalentPointGainMultiplier = "talent_point_gain_multiplier"; // 积怨成怒、怒火翻涌：怒气点（天赋点）获取加成；撤销退款应原额返还。

        // 装备强化
        public const string DressBonusMultiplier = "dress_bonus_multiplier"; // 巧饰虫装、精工战衣：装扮属性加成。
        public const string BagWeightReduction = "bag_weight_reduction"; // 轻装上阵、减负疾行：背包负重减免比例。
        public const string BagCapacityBonus = "bag_capacity_bonus"; // 扩囊增容、巨囊载物：背包额外格数（16→20 为 +4，20→25 为 +5）。

        // 虫群进化
        public const string GroupMovementSpeedMultiplier = "group_movement_speed_multiplier"; // 群足疾行、虫行如风、虫潮奔涌：小蟑螂移动速度加成。
        public const string GroupFoodIntakeMultiplier = "group_food_intake_multiplier"; // 群颚快食、群虫饕食：每只小蟑螂接触食物时的取食量加成。
        public const string GroupEggProductionSpeedMultiplier = "group_egg_production_speed_multiplier"; // 群卵初繁、群卵盛繁、万卵齐生：群体产卵速度加成。
        public const string NestExplorationRangeMultiplier = "nest_exploration_range_multiplier"; // 纸巢拓土、巢域扩张、纸巢帝国：蟑螂巢穴探索范围加成。
        public const string CockroachDeathFood = "cockroach_death_food"; // 同类相食：启用小蟑螂死亡后变成少量食物的能力。
        public const string GroupWallGnawing = "group_wall_gnawing"; // 群颚破壁：启用小蟑螂啃噬墙体的能力。

        // 母体进化
        public const string MovementSpeed = "movement_speed"; // 疾足奔行：母体（玩家个体）移动速度加成。
        public const string IndividualDamageReduction = "individual_damage_reduction"; // 坚韧甲壳、铁壁虫甲：个体受伤减免比例。
        public const string StaminaCostReduction = "stamina_cost_reduction"; // 节力爬行、持久耐劳：体力损耗减免比例。
        public const string PoisonImpactReduction = "poison_impact_reduction"; // 抗毒体质：中毒影响减免比例。
        public const string IndividualEggProductionSpeedMultiplier = "individual_egg_production_speed_multiplier"; // 母体催卵：个体产卵速度加成。
        public const string InstantEggLaying = "instant_egg_laying"; // 瞬息落卵：启用瞬间产卵的能力。
        public const string LethalDamageProtection = "lethal_damage_protection"; // 绝境蜕生：启用抵抗一次致命伤的能力，是否已消耗由玩法记录。
        public const string StaminaRecoveryMultiplier = "stamina_recovery_multiplier"; // 蓄力回元：体力恢复速度加成。
        public const string DetoxSpeedMultiplier = "detox_speed_multiplier"; // 代谢排毒：解毒速度加成。
        public const string IndividualSprint = "individual_sprint"; // 蓄势突进：启用个体冲刺的能力。
        public const string IndividualInstantHatching = "individual_instant_hatching"; // 破卵即生：启用个体瞬间孵化的能力。
    }
}
