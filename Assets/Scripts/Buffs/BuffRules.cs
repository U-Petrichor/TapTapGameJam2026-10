using System.Collections.Generic;

namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 一条「元规则」：<paramref name="AmplifierKey"/> 的值用来放大来自
    /// <paramref name="AmplifiedSource"/> 的百分比 Buff。
    ///
    /// 为什么不做成通用递归（A 放大 B、B 放大 C…）：
    /// 通用递归会出现循环依赖、无法解释的数值爆炸，策划也没法配平。
    /// 这里只允许一张显式的小表，每条规则写明「谁放大谁」，并且放大系数的计算
    /// <b>排除被放大的那个来源自身</b>，从结构上消灭自乘。
    /// </summary>
    public readonly struct BuffAmplifyRule
    {
        /// <summary>提供放大系数的 key（1 + 它的值 = 系数）</summary>
        public readonly BuffKey AmplifierKey;

        /// <summary>被放大的来源。来自这个来源的百分比 Buff 会被乘上放大系数。</summary>
        public readonly BuffSourceKind AmplifiedSource;

        public BuffAmplifyRule(BuffKey amplifierKey, BuffSourceKind amplifiedSource)
        {
            AmplifierKey = amplifierKey;
            AmplifiedSource = amplifiedSource;
        }

        public override string ToString() => $"{AmplifierKey} 放大 {AmplifiedSource}";
    }

    /// <summary>
    /// Buff 系统的全局规则常量。放在一处，改数值不用翻聚合器。
    /// </summary>
    public static class BuffRules
    {
        /// <summary>放大系数的下限，防止有人配负数把效果反过来</summary>
        public const float AmplifyMin = 0.01f;

        /// <summary>放大系数的上限，防止数值爆炸</summary>
        public const float AmplifyMax = 10f;

        /// <summary>「越大越好」类 key 的乘数下限。不许把移速 / 获取率压到 0。</summary>
        public const float GrowFactorMin = 0.05f;

        /// <summary>「越大越好」类 key 的乘数上限。</summary>
        public const float GrowFactorMax = 10f;

        /// <summary>
        /// 「越小越好」类 key 的乘数下限。0.05 表示最多吃掉 95%，
        /// 避免「100% 免伤 / 零体力消耗」把玩法搞崩。
        /// </summary>
        public const float ShrinkFactorMin = 0.05f;

        /// <summary>「越小越好」类 key 的乘数上限。允许超过 1（负减免 = 减益，例如「更容易受伤」）。</summary>
        public const float ShrinkFactorMax = 5f;

        /// <summary>
        /// 放大规则表。<b>装扮加成只放大装扮来源</b>，这是当前唯一一条元规则。
        /// 以后要做「某天赋放大某类装备效果」，往这里加一行即可。
        /// </summary>
        public static readonly IReadOnlyList<BuffAmplifyRule> AmplifyRules = new[]
        {
            new BuffAmplifyRule(BuffKey.DressBonusPercent, BuffSourceKind.Dress),
        };
    }
}
