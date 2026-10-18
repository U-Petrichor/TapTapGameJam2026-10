using System;
using System.Collections.Generic;

namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 一个 <see cref="BuffKey"/> 的静态定义。只放「不随玩家进度变化」的信息：
    /// 方向、稳定名字、显示名。玩家当前的值由 <see cref="BuffAggregator"/> 算，不在这里。
    /// </summary>
    public readonly struct BuffKeyDefinition
    {
        public readonly BuffKey Key;
        public readonly BuffKind Kind;

        /// <summary>存进资产 / 存档 / 配置表的稳定名字（snake_case）。<b>改名等于改存档</b>。</summary>
        public readonly string Id;

        /// <summary>中文显示名，留给将来的 Buff 列表 UI。</summary>
        public readonly string DisplayName;

        public BuffKeyDefinition(BuffKey key, BuffKind kind, string id, string displayName)
        {
            Key = key;
            Kind = kind;
            Id = id;
            DisplayName = displayName;
        }

        public override string ToString() => $"{Key}({Id}, {Kind})";
    }

    /// <summary>
    /// <see cref="BuffKey"/> 的定义表 + 字符串解析。
    ///
    /// 这张表是「字符串 → 枚举」的唯一入口：天赋资产里存的还是 <c>"coin_gain_multiplier"</c> 这种字符串，
    /// 靠 <see cref="TryParse"/> 翻译成枚举；解析失败会被 <see cref="BuffAggregator"/> 记成 warning，
    /// 而不是静默不生效。
    ///
    /// 纯 C#，不引用 UnityEngine，所以能被 Tools/BuffChecks 直接编译验证。
    /// </summary>
    public static class BuffKeys
    {
        /// <summary>
        /// 定义表。<b>下标必须等于枚举值</b>（<see cref="Validate"/> 会检查），所以增删 key 时
        /// 只能加在枚举尾部或同步调整这里的顺序。
        /// </summary>
        private static readonly BuffKeyDefinition[] Definitions =
        {
            // 玩家个体：移动
            Def(BuffKey.PlayerMoveSpeedPercent, BuffKind.Grow, "movement_speed", "移动速度"),
            Def(BuffKey.SprintSpeedPercent, BuffKind.Grow, "sprint_speed", "冲刺速度"),

            // 玩家个体：体力 / 受伤 / 中毒 / 负重
            Def(BuffKey.StaminaMaxPercent, BuffKind.Grow, "stamina_max", "体力上限"),
            Def(BuffKey.StaminaMaxFlat, BuffKind.Flat, "stamina_max_flat", "体力上限固定值"),
            Def(BuffKey.PlayerDamageReduction, BuffKind.Shrink, "individual_damage_reduction", "受伤减免"),
            Def(BuffKey.PoisonImpactReduction, BuffKind.Shrink, "poison_impact_reduction", "中毒影响减免"),
            Def(BuffKey.StaminaCostReduction, BuffKind.Shrink, "stamina_cost_reduction", "体力损耗减免"),
            Def(BuffKey.StaminaRecoveryPercent, BuffKind.Grow, "stamina_recovery_multiplier", "体力恢复速度"),
            Def(BuffKey.DetoxSpeedPercent, BuffKind.Grow, "detox_speed_multiplier", "解毒速度"),
            Def(BuffKey.BagWeightReduction, BuffKind.Shrink, "bag_weight_reduction", "负重减免"),
            Def(BuffKey.BagCapacityTier, BuffKind.Flat, "bag_capacity_bonus", "背包扩容档位"),

            // 虫群
            Def(BuffKey.CockroachSpeedPercent, BuffKind.Grow, "group_movement_speed_multiplier", "虫群移速"),
            Def(BuffKey.CockroachCarryPercent, BuffKind.Grow, "group_food_intake_multiplier", "虫群取食量"),
            Def(BuffKey.CockroachHatchPercent, BuffKind.Grow, "group_egg_production_speed_multiplier", "虫群产卵速度"),
            Def(BuffKey.PlayerEggProductionPercent, BuffKind.Grow, "individual_egg_production_speed_multiplier", "母体产卵速度"),

            // 巢穴
            Def(BuffKey.NestRangePercent, BuffKind.Grow, "nest_exploration_range_multiplier", "巢穴探索范围"),
            Def(BuffKey.NestCapacityFlat, BuffKind.Flat, "nest_capacity_bonus", "巢穴容量"),

            // 资源获取
            Def(BuffKey.CoinGainPercent, BuffKind.Grow, "coin_gain_multiplier", "金币获取"),
            Def(BuffKey.NutritionGainPercent, BuffKind.Grow, "nutrition_gain_multiplier", "营养获取"),
            Def(BuffKey.TalentPointGainPercent, BuffKind.Grow, "talent_point_gain_multiplier", "天赋点获取"),
            Def(BuffKey.CoffeeGainPercent, BuffKind.Grow, "coffee_gain_multiplier", "咖啡获取"),

            // 元属性 / 特殊
            Def(BuffKey.DressBonusPercent, BuffKind.Grow, "dress_bonus_multiplier", "装扮加成"),
            Def(BuffKey.LuckFlat, BuffKind.Flat, "luck", "幸运"),

            // 敌人
            Def(BuffKey.GuardPlayerDetectReduction, BuffKind.Shrink, "guard_player_detect_reduction", "守卫发现距离减免"),

            // 能力型 Tag
            Def(BuffKey.FlagCockroachDeathFood, BuffKind.Flag, "cockroach_death_food", "同类相食"),
            Def(BuffKey.FlagGroupWallGnawing, BuffKind.Flag, "group_wall_gnawing", "群颚破壁"),
            Def(BuffKey.FlagInstantEggLaying, BuffKind.Flag, "instant_egg_laying", "瞬息落卵"),
            Def(BuffKey.FlagLethalDamageProtection, BuffKind.Flag, "lethal_damage_protection", "绝境蜕生"),
            Def(BuffKey.FlagIndividualSprint, BuffKind.Flag, "individual_sprint", "蓄势突进"),
            Def(BuffKey.FlagIndividualInstantHatching, BuffKind.Flag, "individual_instant_hatching", "破卵即生"),
        };

        /// <summary>枚举成员个数。聚合器的数组按它开。</summary>
        public static readonly int Count = Enum.GetValues(typeof(BuffKey)).Length;

        private static readonly Dictionary<string, BuffKey> IdToKey =
            new Dictionary<string, BuffKey>(StringComparer.OrdinalIgnoreCase);

        static BuffKeys()
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                BuffKeyDefinition definition = Definitions[i];
                if (!string.IsNullOrEmpty(definition.Id) && !IdToKey.ContainsKey(definition.Id))
                {
                    IdToKey.Add(definition.Id, definition.Key);
                }
            }
        }

        // 只是为了少写一遍类型名 + 顺手做下标对齐
        private static BuffKeyDefinition Def(BuffKey key, BuffKind kind, string id, string displayName)
            => new BuffKeyDefinition(key, kind, id, displayName);

        /// <summary>取定义。越界的枚举值（例如手工强转）返回一个安全的 Flag 定义，不抛异常。</summary>
        public static BuffKeyDefinition Get(BuffKey key)
        {
            int index = (int)key;
            if (index < 0 || index >= Definitions.Length)
            {
                return new BuffKeyDefinition(key, BuffKind.Flag, key.ToString(), key.ToString());
            }

            return Definitions[index];
        }

        /// <summary>稳定名字（snake_case），写日志和配表用。</summary>
        public static string ToId(BuffKey key) => Get(key).Id;

        /// <summary>方向。判断「这条 modifier 能不能作用在这个 key 上」时用。</summary>
        public static BuffKind KindOf(BuffKey key) => Get(key).Kind;

        /// <summary>是不是能力 Tag（只问有没有，不看数值）。</summary>
        public static bool IsFlag(BuffKey key) => Get(key).Kind == BuffKind.Flag;

        /// <summary>
        /// 字符串 → key。大小写不敏感、忽略首尾空白，解析不了返回 false（调用方负责报出来）。
        /// </summary>
        public static bool TryParse(string id, out BuffKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return IdToKey.TryGetValue(id.Trim(), out key);
        }

        /// <summary>
        /// 自检：下标对齐、名字非空且唯一、名字是 snake_case。返回问题列表，空表示没问题。
        /// 由 Tools/BuffChecks 和编辑器启动时调用；不要在运行时每帧调。
        /// </summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();

            if (Definitions.Length != Count)
            {
                problems.Add($"定义表有 {Definitions.Length} 项，但枚举有 {Count} 个成员");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Definitions.Length; i++)
            {
                BuffKeyDefinition definition = Definitions[i];
                if (!Enum.IsDefined(typeof(BuffKey), definition.Key))
                {
                    problems.Add($"定义表第 {i} 项 {definition.Key} 不是合法的 BuffKey");
                    continue;
                }

                if ((int)definition.Key != i)
                {
                    problems.Add($"定义表第 {i} 项是 {definition.Key}（应该是 {(BuffKey)i}）：下标和枚举值没对齐");
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    problems.Add($"{definition.Key} 没有稳定名字");
                    continue;
                }

                if (!IsSnakeCase(definition.Id))
                {
                    problems.Add($"{definition.Key} 的稳定名字「{definition.Id}」不是 snake_case");
                }

                if (!seen.Add(definition.Id))
                {
                    problems.Add($"稳定名字「{definition.Id}」重复了");
                }
            }

            return problems;
        }

        private static bool IsSnakeCase(string id)
        {
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                {
                    return false;
                }
            }

            // 不许下划线打头 / 结尾 / 连续
            return id.Length > 0 && id[0] != '_' && id[id.Length - 1] != '_' && !id.Contains("__");
        }
    }
}
