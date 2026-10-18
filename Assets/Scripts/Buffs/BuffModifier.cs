using System;

namespace TapTapGameJam.Buffs
{
    /// <summary>一条 Buff 数值到底是「加一个固定值」还是「乘一个百分比」还是「点亮一个能力」。</summary>
    public enum BuffOp
    {
        /// <summary>加固定值：进 Σflat。计数型（档位/容量）只能用它。</summary>
        Flat,

        /// <summary>百分比：进 Π(1±pct)。正负号由 <see cref="BuffKind"/> 决定，见 <see cref="BuffAggregator"/>。</summary>
        Percent,

        /// <summary>能力开关：把对应的 <see cref="BuffKind.Flag"/> key 点亮，Value 被忽略。</summary>
        Flag,
    }

    /// <summary>
    /// 这条 Buff 从哪来。除了排查问题，它还有两个真实用途：
    /// 1. <see cref="BuffRules.AmplifyRules"/> 按来源决定「某类来源的百分比要不要被放大」；
    /// 2. 同来源重复下发时整份替换（见 <see cref="BuffAggregator.SetSource"/>），不必手动 diff。
    /// </summary>
    public enum BuffSourceKind
    {
        /// <summary>科技树天赋（TalentEffectResolver 展开的结果）</summary>
        Talent,

        /// <summary>装扮（头/身/足三个装备槽）</summary>
        Dress,

        /// <summary>临时效果：药水、buff 道具、限时增益</summary>
        Consumable,

        /// <summary>调试/作弊。默认不受装扮加成放大，方便单独验证数值</summary>
        Debug,
    }

    /// <summary>
    /// 一条待聚合的 Buff。所有来源（天赋、装扮、临时效果）都先翻译成这个结构，
    /// 再交给 <see cref="BuffAggregator"/>，游戏逻辑只和聚合结果打交道。
    ///
    /// 用 struct 而不是 class：数量少、创建频繁（每次重建都新建一批），值语义也让「整份替换」的判断更直接。
    /// 字段全是 public：将来装扮配表如果要把它直接序列化进 ScriptableObject，Unity 能认。
    /// </summary>
    [Serializable]
    public struct BuffModifier : IEquatable<BuffModifier>
    {
        public BuffKey Key;
        public BuffOp Op;
        public float Value;
        public BuffSourceKind Source;

        public BuffModifier(BuffKey key, BuffOp op, float value, BuffSourceKind source)
        {
            Key = key;
            Op = op;
            Value = value;
            Source = source;
        }

        /// <summary>百分比增益。0.1 表示 +10%。</summary>
        public static BuffModifier Percent(BuffKey key, float value, BuffSourceKind source = BuffSourceKind.Talent)
            => new BuffModifier(key, BuffOp.Percent, value, source);

        /// <summary>固定值增益。用于档位 / 容量 / 幸运这类计数型 key。</summary>
        public static BuffModifier Flat(BuffKey key, float value, BuffSourceKind source = BuffSourceKind.Talent)
            => new BuffModifier(key, BuffOp.Flat, value, source);

        /// <summary>点亮一个能力 Tag。</summary>
        public static BuffModifier Flag(BuffKey key, BuffSourceKind source = BuffSourceKind.Talent)
            => new BuffModifier(key, BuffOp.Flag, 0f, source);

        /// <summary>
        /// 按 key 的 <see cref="BuffKind"/> 自动选 op。
        ///
        /// <b>天赋和装扮都走这一个入口</b>：配表只填「key + 数值」，是百分比还是固定值由 key 决定，
        /// 策划没有机会选错 op，也不会把 Tag 的 1 当成 +100%。
        /// </summary>
        public static BuffModifier FromValue(BuffKey key, float value, BuffSourceKind source)
        {
            switch (BuffKeys.KindOf(key))
            {
                case BuffKind.Flag:
                    // Tag 只看有没有，数值没有意义
                    return Flag(key, source);
                case BuffKind.Flat:
                    return Flat(key, value, source);
                default:
                    return Percent(key, value, source);
            }
        }

        /// <summary>
        /// 稳定的排序比较（Key → Op → Value）。
        /// 来源层必须用同一套排序：同样的装备 / 天赋进度每次都要产出同样的序列，
        /// 否则 <see cref="BuffAggregator.SetSource"/> 会把「内容没变」误判成「变了」，
        /// 每次刷新都涨 Revision。
        /// </summary>
        public static int CompareStable(BuffModifier a, BuffModifier b)
        {
            int byKey = ((int)a.Key).CompareTo((int)b.Key);
            if (byKey != 0)
            {
                return byKey;
            }

            int byOp = ((int)a.Op).CompareTo((int)b.Op);
            return byOp != 0 ? byOp : a.Value.CompareTo(b.Value);
        }

        public bool Equals(BuffModifier other)
            => Key == other.Key && Op == other.Op && Source == other.Source
               && Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is BuffModifier other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Key;
                hash = (hash * 397) ^ (int)Op;
                hash = (hash * 397) ^ (int)Source;
                hash = (hash * 397) ^ Value.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
            => Op == BuffOp.Flag
                ? $"{Source}:{Key}=Flag"
                : $"{Source}:{Key}{Op}({Value:0.###})";
    }
}
