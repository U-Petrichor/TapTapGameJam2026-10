using System;
using System.Collections.Generic;
using TapTapGameJam.Buffs;

/// <summary>
/// Buff 核心的回归检查。纯 C#，不需要开 Unity：
///   dotnet run --project Tools/BuffChecks
/// 这里只验证「公式和聚合规则」，不验证任何 Unity 侧接线。
/// </summary>
internal static class Program
{
    private const float Epsilon = 1e-4f;
    private static int _checks;

    private static void Main()
    {
        CheckKeyTable();
        CheckEmptyAggregator();
        CheckGrowStacking();
        CheckShrinkStacking();
        CheckFlatOps();
        CheckPercentNegative();
        CheckFlags();
        CheckSourceReplacement();
        CheckAmplify();
        CheckAmplifyDoesNotSelfMultiply();
        CheckClamps();
        CheckOpMismatchWarnings();
        CheckOrderIndependence();
        CheckTalentBuffSource();
        CheckTalentBuffSourceIsStable();
        CheckLegacyTalentKeysMatchBuffKeys();

        Console.WriteLine($"PASS: {_checks} buff checks.");
    }

    // ---------------- key 表 ----------------

    private static void CheckKeyTable()
    {
        List<string> problems = BuffKeys.Validate();
        Check(problems.Count == 0,
            "BuffKeys.Validate() 报问题：" + string.Join(" | ", problems));

        foreach (BuffKey key in Enum.GetValues(typeof(BuffKey)))
        {
            string id = BuffKeys.ToId(key);
            Check(!string.IsNullOrEmpty(id), $"{key} 没有稳定名字");

            Check(BuffKeys.TryParse(id, out BuffKey parsed), $"TryParse 解析不了 {key} 的名字 {id}");
            Check(parsed == key, $"TryParse({id}) 解析成了 {parsed}，应该是 {key}");

            // 大小写不敏感 + 忽略空白，资产里手写名字时容错
            Check(BuffKeys.TryParse("  " + id.ToUpperInvariant() + " ", out BuffKey upper), $"大小写容错失败：{id}");
            Check(upper == key, $"大小写容错解析成了 {upper}");
        }

        Check(!BuffKeys.TryParse("not_a_real_key", out _), "未知名字应该解析失败");
        Check(!BuffKeys.TryParse("", out _), "空名字应该解析失败");
        Check(!BuffKeys.TryParse(null, out _), "null 应该解析失败");

        // 既有天赋资产里的字符串必须继续能解析（改名等于改存档）
        Check(BuffKeys.TryParse("coin_gain_multiplier", out BuffKey coin) && coin == BuffKey.CoinGainPercent,
            "历史 key coin_gain_multiplier 映射错了");
        Check(BuffKeys.TryParse("group_egg_production_speed_multiplier", out BuffKey hatch)
              && hatch == BuffKey.CockroachHatchPercent,
            "历史 key group_egg_production_speed_multiplier 映射错了");
        Check(BuffKeys.TryParse("nest_exploration_range_multiplier", out BuffKey range)
              && range == BuffKey.NestRangePercent,
            "历史 key nest_exploration_range_multiplier 映射错了");
        Check(BuffKeys.TryParse("group_wall_gnawing", out BuffKey gnaw)
              && gnaw == BuffKey.FlagGroupWallGnawing,
            "历史 key group_wall_gnawing 映射错了");
    }

    // ---------------- 空状态 ----------------

    private static void CheckEmptyAggregator()
    {
        var buffs = new BuffAggregator();

        Check(Approx(buffs.Apply(BuffKey.PlayerMoveSpeedPercent, 4.5f), 4.5f), "空聚合器不该改变基准值");
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), 1f), "空聚合器系数应该是 1");
        Check(Approx(buffs.GetFlat(BuffKey.LuckFlat), 0f), "空聚合器固定值应该是 0");
        Check(!buffs.Has(BuffKey.FlagGroupWallGnawing), "空聚合器不该点亮任何 Tag");
        Check(buffs.Revision == 0, "空聚合器 Revision 应该是 0");
        Check(buffs.Warnings.Count == 0, "空聚合器不该有 warning");
    }

    // ---------------- 加法固定值 + 乘法百分比 ----------------

    private static void CheckGrowStacking()
    {
        var buffs = new BuffAggregator();

        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, 0.1f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), 1.1f), "单个 +10% 应该是 ×1.1");
        Check(Approx(buffs.Apply(BuffKey.PlayerMoveSpeedPercent, 10f), 11f), "10 × 1.1 应该是 11");

        // 乘法叠加：两个 +10% 是 ×1.21，不是 ×1.20
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, 0.1f),
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, 0.1f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), 1.21f), "两个 +10% 应该是 ×1.21（乘法叠加）");

        // 固定值加在百分比之前
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Flat(BuffKey.PlayerMoveSpeedPercent, 2f),
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, 0.5f),
        });
        Check(Approx(buffs.Apply(BuffKey.PlayerMoveSpeedPercent, 10f), 18f),
            "(10 + 2) × 1.5 应该是 18");
    }

    private static void CheckShrinkStacking()
    {
        var buffs = new BuffAggregator();

        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, 0.5f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerDamageReduction), 0.5f), "50% 减免后应该剩 ×0.5");

        // 乘法叠加：两个 50% 减免剩 ×0.25，而不是「免疫」
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, 0.5f),
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, 0.5f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerDamageReduction), 0.25f),
            "两个 50% 减免应该剩 ×0.25");
        Check(Approx(buffs.Apply(BuffKey.PlayerDamageReduction, 20f), 5f),
            "受 20 点伤 × 0.25 应该是 5");
    }

    // ---------------- 计数型 key ----------------

    private static void CheckFlatOps()
    {
        var buffs = new BuffAggregator();

        // 背包扩容档位：只有固定值有意义
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Flat(BuffKey.BagCapacityTier, 1f),
        });
        Check(buffs.ApplyInt(BuffKey.BagCapacityTier, 0) == 1, "一级扩容应该是档位 1");
        Check(Approx(buffs.GetFactor(BuffKey.BagCapacityTier), 1f), "计数型 key 的系数恒为 1");

        // 两个天赋各 +1 档 → 2 档
        var both = new BuffAggregator();
        both.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Flat(BuffKey.BagCapacityTier, 1f),
            BuffModifier.Flat(BuffKey.BagCapacityTier, 1f),
        });
        Check(both.ApplyInt(BuffKey.BagCapacityTier, 0) == 2, "两个 +1 档应该是 2 档");

        // 幸运是固定值，负幸运（诅咒）也要能表达
        both.SetSource(BuffSourceKind.Dress, new List<BuffModifier>
        {
            BuffModifier.Flat(BuffKey.LuckFlat, -3f),
        });
        Check(both.ApplyInt(BuffKey.LuckFlat, 10) == 7, "10 幸运 -3 应该是 7");

        // 计数型 key 上配百分比：要报 warning 并忽略，不能静默乘
        both.SetSource(BuffSourceKind.Consumable, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.BagCapacityTier, 0.5f),
        });
        Check(Approx(both.Apply(BuffKey.BagCapacityTier, 0f), 2f),
            "计数型 key 上的百分比必须被忽略（仍是 2 档）");
        Check(both.Warnings.Count > 0, "计数型 key 上配百分比必须产生 warning");
    }

    private static void CheckPercentNegative()
    {
        // 减益：Grow 类配负数就是减速
        var buffs = new BuffAggregator();
        buffs.SetSource(BuffSourceKind.Consumable, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, -0.1f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), 0.9f), "移动速度 -10% 应该是 ×0.9");

        // Shrink 类配负数就是「更容易受伤」，系数可以超过 1
        buffs.SetSource(BuffSourceKind.Consumable, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, -0.1f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerDamageReduction), 1.1f),
            "受伤减免 -10% 应该是 ×1.1（受到更多伤）");
    }

    // ---------------- Tag ----------------

    private static void CheckFlags()
    {
        var buffs = new BuffAggregator();

        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Flag(BuffKey.FlagGroupWallGnawing),
            BuffModifier.Flag(BuffKey.FlagCockroachDeathFood),
        });
        Check(buffs.Has(BuffKey.FlagGroupWallGnawing), "破壁 Tag 应该点亮");
        Check(buffs.Has(BuffKey.FlagCockroachDeathFood), "同类相食 Tag 应该点亮");
        Check(!buffs.Has(BuffKey.FlagLethalDamageProtection), "没下发的 Tag 不该点亮");
        Check(!buffs.Has(BuffKey.PlayerMoveSpeedPercent), "数值 key 不是 Tag，Has 应该是 false");

        // Tag 对 Apply 没有影响
        Check(Approx(buffs.Apply(BuffKey.FlagGroupWallGnawing, 7f), 7f), "Tag key 的 Apply 应该原样返回");

        // 多个来源，任一来源点亮即可
        buffs.SetSource(BuffSourceKind.Dress, new List<BuffModifier>
        {
            BuffModifier.Flag(BuffKey.FlagLethalDamageProtection),
        });
        Check(buffs.Has(BuffKey.FlagLethalDamageProtection), "装扮来的 Tag 也要点亮");
        Check(buffs.Has(BuffKey.FlagGroupWallGnawing), "换来源不该清掉别的来源的 Tag");

        // 撤掉来源后 Tag 跟着灭
        Check(buffs.RemoveSource(BuffSourceKind.Dress), "RemoveSource 应该报告发生了变化");
        Check(!buffs.Has(BuffKey.FlagLethalDamageProtection), "撤掉来源后 Tag 应该灭");
        Check(buffs.Has(BuffKey.FlagGroupWallGnawing), "别的来源的 Tag 不该被连带清掉");

        // Flag op 打到数值 key 上：警告 + 忽略
        buffs.SetSource(BuffSourceKind.Debug, new List<BuffModifier>
        {
            BuffModifier.Flag(BuffKey.PlayerMoveSpeedPercent),
        });
        Check(buffs.Warnings.Count > 0, "Flag 打到数值 key 上应该产生 warning");
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), 1f),
            "Flag 打到数值 key 上不该改变数值");
    }

    // ---------------- 来源整份替换 ----------------

    private static void CheckSourceReplacement()
    {
        var buffs = new BuffAggregator();

        var first = new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.2f),
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.2f),
        };
        Check(buffs.SetSource(BuffSourceKind.Talent, first), "第一次下发应该报告变化");
        int revision = buffs.Revision;
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.44f), "两个 +20% 应该是 ×1.44");

        // 同一份内容再下发一次：不该重建、不该涨 Revision
        Check(!buffs.SetSource(BuffSourceKind.Talent, first), "内容没变不该报告变化");
        Check(buffs.Revision == revision, "内容没变不该涨 Revision");

        // 换成更少的一条：旧的要彻底消失，不能留残影
        Check(buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.2f),
        }), "内容变了应该报告变化");
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.2f), "整份替换后应该只剩一条 +20%");
        Check(buffs.Revision == revision + 1, "内容变了应该涨 1 次 Revision");

        // 空列表等价于清空该来源
        Check(buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>()), "换成空列表应该报告变化");
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1f), "换成空列表后应该没有加成");
        Check(!buffs.HasSource(BuffSourceKind.Talent), "空来源不该算 HasSource");
        Check(!buffs.RemoveSource(BuffSourceKind.Talent), "空来源 RemoveSource 不该报告变化");
    }

    // ---------------- 元属性：装扮加成 ----------------

    private static void CheckAmplify()
    {
        var buffs = new BuffAggregator();

        // 天赋给 +50% 装扮加成；装扮给 +10% 金币
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.DressBonusPercent, 0.5f),
        });
        buffs.SetSource(BuffSourceKind.Dress, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.1f),
        });

        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.15f),
            "装扮 +10% 金币，在 +50% 装扮加成下应该是 ×1.15");
        Check(Approx(buffs.ApplyInt(BuffKey.CoinGainPercent, 100), 115),
            "100 金币在 +50% 装扮加成下应该是 115");

        // 天赋来源的金币不受装扮加成放大
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.DressBonusPercent, 0.5f),
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.1f),
        });
        // 天赋 1.1 × 装扮 1.15 = 1.265
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.265f),
            "天赋金币只吃自己的 +10%，装扮金币吃 +50% 放大：应该是 ×1.265");

        // 没点装扮加成天赋时，装扮按原始值生效
        var plain = new BuffAggregator();
        plain.SetSource(BuffSourceKind.Dress, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.1f),
        });
        Check(Approx(plain.GetFactor(BuffKey.CoinGainPercent), 1.1f), "没有装扮加成时应该是原始 +10%");
    }

    private static void CheckAmplifyDoesNotSelfMultiply()
    {
        var buffs = new BuffAggregator();

        // 装扮自己同时给「装扮加成」和「金币」：装扮加成不能被它自己抬起来
        buffs.SetSource(BuffSourceKind.Dress, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.DressBonusPercent, 0.5f),
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.1f),
        });

        Check(Approx(buffs.GetFactor(BuffKey.DressBonusPercent), 1.5f),
            "装扮自己给的装扮加成不该自乘（应为 ×1.5，不是 ×2.25）");
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.1f),
            "没有外部装扮加成时，装扮的金币加成不该被放大");

        // 天赋也给了装扮加成时，两边的加成相乘（天赋 1.5 × 装扮 1.5）
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.DressBonusPercent, 0.5f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.DressBonusPercent), 2.25f),
            "天赋 +50% 与装扮 +50% 装扮加成应该相乘为 ×2.25");
        // 放大系数 = 天赋那一份 = 1.5；装扮金币 +10% × 1.5 = +15%
        Check(Approx(buffs.GetFactor(BuffKey.CoinGainPercent), 1.15f),
            "装扮金币在 +50% 装扮加成下应该是 ×1.15");
    }

    // ---------------- 夹取 ----------------

    private static void CheckClamps()
    {
        var buffs = new BuffAggregator();

        // 极限减免不该变成免疫
        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, 0.9f),
            BuffModifier.Percent(BuffKey.PlayerDamageReduction, 0.9f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerDamageReduction), BuffRules.ShrinkFactorMin),
            "两个 90% 减免应该被夹到下限，而不是免疫");
        Check(buffs.Apply(BuffKey.PlayerDamageReduction, 100f) > 0f, "减免夹取后仍然要受到伤害");

        // 移速被压到负值也要有下限
        buffs.SetSource(BuffSourceKind.Consumable, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, -5f),
        });
        Check(Approx(buffs.GetFactor(BuffKey.PlayerMoveSpeedPercent), BuffRules.GrowFactorMin),
            "移速被压到 -500% 应该夹到下限，而不是负数");

        // 上限
        var stacked = new BuffAggregator();
        stacked.SetSource(BuffSourceKind.Debug, new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.PlayerMoveSpeedPercent, 100f),
        });
        Check(Approx(stacked.GetFactor(BuffKey.PlayerMoveSpeedPercent), BuffRules.GrowFactorMax),
            "移速被堆到 +10000% 应该夹到上限");
    }

    // ---------------- 配错表 ----------------

    private static void CheckOpMismatchWarnings()
    {
        var buffs = new BuffAggregator();

        buffs.SetSource(BuffSourceKind.Talent, new List<BuffModifier>
        {
            BuffModifier.Flat(BuffKey.FlagInstantEggLaying, 1f),          // Tag 上配固定值
            new BuffModifier((BuffKey)9999, BuffOp.Percent, 0.1f, BuffSourceKind.Talent), // 越界 key
        });

        Check(buffs.Warnings.Count >= 2, "Tag 上配固定值 + 越界 key 都应该产生 warning");
        Check(Approx(buffs.Apply(BuffKey.PlayerMoveSpeedPercent, 5f), 5f), "错误配置不该污染别的 key");
        Check(!buffs.Has(BuffKey.FlagInstantEggLaying), "Tag 上配固定值不该点亮 Tag");
    }

    // ---------------- 顺序无关 ----------------

    private static void CheckOrderIndependence()
    {
        var talent = new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CockroachSpeedPercent, 0.2f),
            BuffModifier.Percent(BuffKey.DressBonusPercent, 0.5f),
        };
        var dress = new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CockroachSpeedPercent, 0.1f),
            BuffModifier.Percent(BuffKey.CoinGainPercent, 0.2f),
        };
        var consumable = new List<BuffModifier>
        {
            BuffModifier.Percent(BuffKey.CockroachSpeedPercent, 0.05f),
            BuffModifier.Flag(BuffKey.FlagGroupWallGnawing),
        };

        var forward = new BuffAggregator();
        forward.SetSource(BuffSourceKind.Talent, talent);
        forward.SetSource(BuffSourceKind.Dress, dress);
        forward.SetSource(BuffSourceKind.Consumable, consumable);

        var backward = new BuffAggregator();
        backward.SetSource(BuffSourceKind.Consumable, consumable);
        backward.SetSource(BuffSourceKind.Dress, dress);
        backward.SetSource(BuffSourceKind.Talent, talent);

        Check(Approx(forward.GetFactor(BuffKey.CockroachSpeedPercent), backward.GetFactor(BuffKey.CockroachSpeedPercent)),
            "虫群移速不该依赖下发顺序");
        Check(Approx(forward.GetFactor(BuffKey.CoinGainPercent), backward.GetFactor(BuffKey.CoinGainPercent)),
            "金币（含装扮放大）不该依赖下发顺序");
        Check(Approx(forward.GetFactor(BuffKey.DressBonusPercent), backward.GetFactor(BuffKey.DressBonusPercent)),
            "装扮加成不该依赖下发顺序");
        Check(forward.Has(BuffKey.FlagGroupWallGnawing) == backward.Has(BuffKey.FlagGroupWallGnawing),
            "Tag 不该依赖下发顺序");

        // 三个来源的虫群移速：天赋 1.2 × 装扮（+10% 被 +50% 装扮加成放大成 1.15）× 临时 1.05
        Check(Approx(forward.GetFactor(BuffKey.CockroachSpeedPercent), 1.2f * 1.15f * 1.05f),
            "三个来源的虫群移速应该连乘，且装扮那一份吃装扮加成");
    }

    // ---------------- 天赋 → Buff 的翻译 ----------------

    private static void CheckTalentBuffSource()
    {
        var resolver = new TalentTree.Effects.TalentEffectResolver(new List<TalentTree.Effects.TalentEffect>
        {
            TalentTree.Effects.TalentEffect.Make("movement_speed", 0.1f),      // Grow  → Percent
            TalentTree.Effects.TalentEffect.Make("bag_capacity_bonus", 1f),    // Flat  → Flat（档位）
            TalentTree.Effects.TalentEffect.Make("group_wall_gnawing", 1f),    // Tag   → Flag（数值丢弃）
            TalentTree.Effects.TalentEffect.Make("not_a_real_key", 0.5f),      // 未知 → 收集，不静默丢
        });

        var source = new TalentBuffSource(resolver);
        List<BuffModifier> mods = new List<BuffModifier>(source.Build());

        Check(mods.Count == 3, $"三条有效效果应该产出 3 条 modifier，实际 {mods.Count}");
        Check(source.UnknownKeys.Count == 1 && source.UnknownKeys[0] == "not_a_real_key",
            "未知 key 应该被收集起来，而不是静默丢掉");

        Check(Has(mods, BuffKey.PlayerMoveSpeedPercent, BuffOp.Percent), "百分比 key 应该翻成 Percent");
        Check(Has(mods, BuffKey.BagCapacityTier, BuffOp.Flat), "计数 key 应该翻成 Flat");
        Check(Has(mods, BuffKey.FlagGroupWallGnawing, BuffOp.Flag), "Tag key 应该翻成 Flag");
        Check(!Has(mods, BuffKey.FlagGroupWallGnawing, BuffOp.Percent),
            "Tag 上的数值（1）不能被当成百分比");

        // 端到端：天赋 → modifier → 聚合 → 游戏数值
        var buffs = new BuffAggregator();
        buffs.SetSource(BuffSourceKind.Talent, source.Build());
        Check(Approx(buffs.Apply(BuffKey.PlayerMoveSpeedPercent, 10f), 11f), "天赋 +10% 移速应该让 10 变成 11");
        Check(buffs.ApplyInt(BuffKey.BagCapacityTier, 0) == 1, "天赋 1 档扩容应该让档位变成 1");
        Check(buffs.Has(BuffKey.FlagGroupWallGnawing), "天赋 Tag 应该在聚合结果里点亮");
        Check(buffs.Warnings.Count == 0, "正常配置不该产生 warning：" + string.Join(" | ", buffs.Warnings));
    }

    private static void CheckTalentBuffSourceIsStable()
    {
        var effects = new List<TalentTree.Effects.TalentEffect>
        {
            TalentTree.Effects.TalentEffect.Make("coin_gain_multiplier", 0.1f),
            TalentTree.Effects.TalentEffect.Make("movement_speed", 0.2f),
            TalentTree.Effects.TalentEffect.Make("group_movement_speed_multiplier", 0.05f),
        };
        var source = new TalentBuffSource(new TalentTree.Effects.TalentEffectResolver(effects));
        var buffs = new BuffAggregator();

        buffs.SetSource(BuffSourceKind.Talent, source.Build());
        int revision = buffs.Revision;

        // 同样输入再翻译一次：顺序必须完全一致，否则聚合器会误判成「变了」，
        // 每次重算都会涨 Revision → UI 白刷、快照白算。
        Check(!buffs.SetSource(BuffSourceKind.Talent, source.Build()),
            "同样的天赋进度重复翻译，不该让聚合器认为内容变了");
        Check(buffs.Revision == revision, "重复翻译不该涨 Revision");

        // 输入顺序换了，产出顺序仍应一致
        effects.Reverse();
        Check(!buffs.SetSource(BuffSourceKind.Talent, source.Build()),
            "天赋解析顺序变化不该影响翻译结果的顺序");
    }

    private static bool Has(List<BuffModifier> mods, BuffKey key, BuffOp op)
    {
        for (int i = 0; i < mods.Count; i++)
        {
            if (mods[i].Key == key && mods[i].Op == op)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------- 历史 key 表不能漂 ----------------

    /// <summary>
    /// TalentEffectKey 里那 23 个常量是「资产里可能出现的字符串」的对照清单。
    /// 权威来源已经换成 BuffKeys，这两个表一旦漂了，天赋资产就会静默失效——
    /// 所以这里用反射把它们逐条对一遍。
    /// </summary>
    private static void CheckLegacyTalentKeysMatchBuffKeys()
    {
        var fields = typeof(TalentTree.Effects.TalentEffectKey).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var seen = new HashSet<BuffKey>();
        int checkedCount = 0;

        foreach (System.Reflection.FieldInfo field in fields)
        {
            if (!field.IsLiteral || field.FieldType != typeof(string))
            {
                continue;
            }

            string id = (string)field.GetRawConstantValue();
            checkedCount++;

            Check(BuffKeys.TryParse(id, out BuffKey key),
                $"TalentEffectKey.{field.Name} = \"{id}\" 在 BuffKeys 里没有对应的 key");
            Check(BuffKeys.ToId(key) == id,
                $"TalentEffectKey.{field.Name} = \"{id}\" 和 BuffKeys 里的 \"{BuffKeys.ToId(key)}\" 不一致");
            Check(seen.Add(key),
                $"TalentEffectKey.{field.Name} 和别的常量指向了同一个 BuffKey（{key}）");
        }

        Check(checkedCount >= 23, $"TalentEffectKey 的常量数量看起来少了（{checkedCount} 个）");
    }

    // ---------------- 工具 ----------------

    private static bool Approx(float actual, float expected)
        => Math.Abs(actual - expected) <= Epsilon * Math.Max(1f, Math.Abs(expected));

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
