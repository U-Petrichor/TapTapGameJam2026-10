using System;
using System.Collections.Generic;

namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// Buff 系统的核心：把「所有来源的所有 modifier」压成「每个 key 一个最终系数」。
    ///
    /// <b>纯 C#，不引用 UnityEngine</b>，所以能被 Tools/BuffChecks 直接跑起来验证公式。
    ///
    /// 合成规则（顺序写死，游戏逻辑不许自己另拼一套）：
    /// <code>
    /// Grow   : final = (base + Σflat) * Π(1 + pct)
    /// Shrink : final = (base + Σflat) * Π(1 - pct)
    /// Flat   : final = (base + Σflat)          // 档位 / 容量 / 幸运
    /// Flag   : 只回答 Has(key)，不参与数值
    /// </code>
    ///
    /// 百分比按 <b>乘法叠加</b>：两个 +10% 得到 ×1.21，不是 ×1.20。
    /// 减免同理：两个 50% 减免后还剩 ×0.25，不会出现「两个 50% 就免疫」。
    ///
    /// 每个来源（<see cref="BuffSourceKind"/>）单独攒一份系数，最后再相乘。
    /// 这样同一份来源可以整份替换（见 <see cref="SetSource"/>），元规则也能按来源挑拣。
    /// </summary>
    public sealed class BuffAggregator
    {
        private static readonly BuffSourceKind[] SourceKinds =
            (BuffSourceKind[])Enum.GetValues(typeof(BuffSourceKind));

        private static readonly int SourceCount = SourceKinds.Length;
        private static readonly int KeyCount = BuffKeys.Count;

        /// <summary>
        /// 在 <see cref="BuffRules.AmplifyRules"/> 里担任放大器的 key。这些 key 自己<b>永远不被放大</b>：
        /// 它是系数，不是数值，被放大只会让数值层层复利、无法解释。
        /// </summary>
        private static readonly bool[] IsAmplifierKey = BuildAmplifierKeyMask();

        /// <summary>[source][key]：该来源在该 key 上的系数 Π(1±pct)，初始 1</summary>
        private readonly float[][] _sourceFactor;

        /// <summary>[source][key]：该来源在该 key 上的固定值之和</summary>
        private readonly float[][] _sourceFlat;

        /// <summary>[source][key]：该来源有没有点亮这个 Tag</summary>
        private readonly bool[][] _sourceFlag;

        /// <summary>聚合后的最终系数（Grow / Shrink 用）</summary>
        private readonly float[] _factor;

        /// <summary>聚合后的固定值之和</summary>
        private readonly float[] _flat;

        /// <summary>聚合后的 Tag 集合</summary>
        private readonly bool[] _flag;

        private readonly List<BuffModifier>[] _modifiers;
        private readonly List<string> _warnings = new List<string>();

        /// <summary>每次内容真的变了就 +1。消费方用它判断「要不要重建快照 / 刷 UI」。</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// 上一次重建时发现的问题（未知 key 由来源层负责报，这里报的是「op 和 key 类型不匹配」）。
        /// 配错表不会崩，但一定要在编辑器里打出来，否则就是「改了配置没生效」。
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        public BuffAggregator()
        {
            _sourceFactor = new float[SourceCount][];
            _sourceFlat = new float[SourceCount][];
            _sourceFlag = new bool[SourceCount][];
            _modifiers = new List<BuffModifier>[SourceCount];
            _factor = new float[KeyCount];
            _flat = new float[KeyCount];
            _flag = new bool[KeyCount];

            for (int s = 0; s < SourceCount; s++)
            {
                _sourceFactor[s] = new float[KeyCount];
                _sourceFlat[s] = new float[KeyCount];
                _sourceFlag[s] = new bool[KeyCount];
                _modifiers[s] = new List<BuffModifier>();
            }

            Rebuild();
        }

        #region 来源维护

        /// <summary>
        /// 整份替换某个来源的 modifier。返回内容是否真的变了。
        /// 「整份替换」而不是「增删单条」：天赋/装备天然就是全量快照，
        /// 增量维护反而要处理「哪条对应哪条」的问题，容易留残影。
        /// </summary>
        public bool SetSource(BuffSourceKind source, IEnumerable<BuffModifier> modifiers)
        {
            int index = IndexOf(source);
            if (index < 0)
            {
                AddWarning($"[BuffAggregator] 未知来源 {source}");
                return false;
            }

            List<BuffModifier> target = _modifiers[index];
            if (SameContent(target, modifiers))
            {
                return false;
            }

            target.Clear();
            if (modifiers != null)
            {
                foreach (BuffModifier modifier in modifiers)
                {
                    target.Add(modifier);
                }
            }

            Rebuild();
            Revision++;
            return true;
        }

        /// <summary>清空某个来源（例如天赋全部撤销、装扮全部脱下）。</summary>
        public bool RemoveSource(BuffSourceKind source)
        {
            int index = IndexOf(source);
            if (index < 0 || _modifiers[index].Count == 0)
            {
                return false;
            }

            _modifiers[index].Clear();
            Rebuild();
            Revision++;
            return true;
        }

        /// <summary>清空所有来源。</summary>
        public void Clear()
        {
            bool changed = false;
            for (int s = 0; s < SourceCount; s++)
            {
                if (_modifiers[s].Count > 0)
                {
                    _modifiers[s].Clear();
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            Rebuild();
            Revision++;
        }

        public bool HasSource(BuffSourceKind source)
        {
            int index = IndexOf(source);
            return index >= 0 && _modifiers[index].Count > 0;
        }

        /// <summary>某个来源当前的 modifier 副本（排查问题 / 做 Buff 列表 UI 用）。</summary>
        public IReadOnlyList<BuffModifier> GetModifiers(BuffSourceKind source)
        {
            int index = IndexOf(source);
            return index >= 0 ? _modifiers[index] : Array.Empty<BuffModifier>();
        }

        #endregion

        #region 读取

        /// <summary>
        /// 把基准值过一遍 Buff。所有游戏数值都应该走这里，而不是自己乘。
        /// 对 <see cref="BuffKind.Flag"/> 的 key 无意义，原样返回。
        /// </summary>
        public float Apply(BuffKey key, float baseValue)
        {
            int index = (int)key;
            if (index < 0 || index >= KeyCount)
            {
                return baseValue;
            }

            switch (BuffKeys.KindOf(key))
            {
                case BuffKind.Flag:
                    return baseValue;
                case BuffKind.Flat:
                    return baseValue + _flat[index];
                default:
                    return (baseValue + _flat[index]) * _factor[index];
            }
        }

        /// <summary>Apply 的整数版（四舍五入）。金币、格数、档位这类用它。</summary>
        public int ApplyInt(BuffKey key, int baseValue)
        {
            float result = Apply(key, baseValue);
            return (int)Math.Round(result, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 只取乘数（不含固定值）。给「已经在别处算过基准值」的地方用，
        /// 例如蟑螂的速度快照：SpeedMul 直接乘 _settings.Speed。
        /// </summary>
        public float GetFactor(BuffKey key)
        {
            int index = (int)key;
            if (index < 0 || index >= KeyCount)
            {
                return 1f;
            }

            BuffKind kind = BuffKeys.KindOf(key);
            return kind == BuffKind.Grow || kind == BuffKind.Shrink ? _factor[index] : 1f;
        }

        /// <summary>只取固定值之和。</summary>
        public float GetFlat(BuffKey key)
        {
            int index = (int)key;
            return index < 0 || index >= KeyCount ? 0f : _flat[index];
        }

        /// <summary>Tag 查询。只有 <see cref="BuffKind.Flag"/> 的 key 会返回 true。</summary>
        public bool Has(BuffKey key)
        {
            int index = (int)key;
            return index >= 0 && index < KeyCount && _flag[index];
        }

        #endregion

        #region 重建

        private void Rebuild()
        {
            _warnings.Clear();

            for (int s = 0; s < SourceCount; s++)
            {
                float[] factor = _sourceFactor[s];
                float[] flat = _sourceFlat[s];
                bool[] flag = _sourceFlag[s];
                for (int k = 0; k < KeyCount; k++)
                {
                    factor[k] = 1f;
                    flat[k] = 0f;
                    flag[k] = false;
                }

                List<BuffModifier> list = _modifiers[s];
                for (int i = 0; i < list.Count; i++)
                {
                    Accumulate(s, list[i]);
                }
            }

            float[] amplify = ResolveAmplifiers();

            for (int k = 0; k < KeyCount; k++)
            {
                BuffKind kind = BuffKeys.Get((BuffKey)k).Kind;
                float factor = 1f;
                float flat = 0f;
                bool flag = false;

                for (int s = 0; s < SourceCount; s++)
                {
                    // 放大只作用于百分比：固定值（档位 / 容量）被乘上 0.5 会变成「半格」，没有意义。
                    // 放大器 key 自己也不被放大，否则数值会复利，配平完全不可预期。
                    float raw = _sourceFactor[s][k];
                    float factorOfSource = IsAmplifierKey[k]
                        ? raw
                        : 1f + (raw - 1f) * amplify[s];
                    factor *= factorOfSource;

                    flat += _sourceFlat[s][k];
                    flag |= _sourceFlag[s][k];
                }

                _factor[k] = ClampByKind(kind, factor);
                _flat[k] = flat;
                _flag[k] = flag;
            }
        }

        private void Accumulate(int sourceIndex, BuffModifier modifier)
        {
            int keyIndex = (int)modifier.Key;
            if (keyIndex < 0 || keyIndex >= KeyCount)
            {
                AddWarning($"[BuffAggregator] {(BuffSourceKind)sourceIndex} 下发了越界的 key {(int)modifier.Key}，已忽略");
                return;
            }

            BuffKind kind = BuffKeys.KindOf(modifier.Key);

            switch (modifier.Op)
            {
                case BuffOp.Flag:
                    if (kind != BuffKind.Flag)
                    {
                        AddWarning($"[BuffAggregator] {BuffKeys.ToId(modifier.Key)} 是 {kind}，不能下发 Flag，已忽略");
                        return;
                    }

                    _sourceFlag[sourceIndex][keyIndex] = true;
                    return;

                case BuffOp.Flat:
                    if (kind == BuffKind.Flag)
                    {
                        AddWarning($"[BuffAggregator] {BuffKeys.ToId(modifier.Key)} 是能力 Tag，不能下发固定值，已忽略");
                        return;
                    }

                    _sourceFlat[sourceIndex][keyIndex] += modifier.Value;
                    return;

                case BuffOp.Percent:
                    if (kind == BuffKind.Flat)
                    {
                        // 档位 / 容量 / 幸运是计数，乘百分比没有定义。配错了要喊出来。
                        AddWarning($"[BuffAggregator] {BuffKeys.ToId(modifier.Key)} 是计数型 key，不能用百分比，已忽略");
                        return;
                    }

                    if (kind == BuffKind.Flag)
                    {
                        AddWarning($"[BuffAggregator] {BuffKeys.ToId(modifier.Key)} 是能力 Tag，不能用百分比，已忽略");
                        return;
                    }

                    // Shrink 类存的是「减免多少」，取负号后就能和 Grow 走同一个累乘。
                    float signed = kind == BuffKind.Shrink ? -modifier.Value : modifier.Value;
                    float[] factor = _sourceFactor[sourceIndex];
                    factor[keyIndex] *= 1f + signed;
                    return;

                default:
                    AddWarning($"[BuffAggregator] {BuffKeys.ToId(modifier.Key)} 带了未知的 BuffOp {modifier.Op}，已忽略");
                    return;
            }
        }

        /// <summary>
        /// 算每个来源的放大系数。默认 1（不放大）。
        ///
        /// 关键点：算某条规则的放大系数时，<b>跳过被放大的那个来源自己的贡献</b>。
        /// 所以「装扮给的装扮加成」不会把这个系数自己抬起来——从结构上消灭自乘，
        /// 不需要运行时防环。
        /// </summary>
        private float[] ResolveAmplifiers()
        {
            var amplify = new float[SourceCount];
            for (int s = 0; s < SourceCount; s++)
            {
                amplify[s] = 1f;
            }

            IReadOnlyList<BuffAmplifyRule> rules = BuffRules.AmplifyRules;
            for (int r = 0; r < rules.Count; r++)
            {
                BuffAmplifyRule rule = rules[r];
                int targetSource = IndexOf(rule.AmplifiedSource);
                int amplifierKey = (int)rule.AmplifierKey;
                if (targetSource < 0 || amplifierKey < 0 || amplifierKey >= KeyCount)
                {
                    continue;
                }

                float product = 1f;
                for (int s = 0; s < SourceCount; s++)
                {
                    if (s == targetSource)
                    {
                        continue;
                    }

                    product *= _sourceFactor[s][amplifierKey];
                }

                amplify[targetSource] = Clamp(product, BuffRules.AmplifyMin, BuffRules.AmplifyMax);
            }

            return amplify;
        }

        private static float ClampByKind(BuffKind kind, float factor)
        {
            switch (kind)
            {
                case BuffKind.Grow:
                    return Clamp(factor, BuffRules.GrowFactorMin, BuffRules.GrowFactorMax);
                case BuffKind.Shrink:
                    return Clamp(factor, BuffRules.ShrinkFactorMin, BuffRules.ShrinkFactorMax);
                default:
                    return 1f;
            }
        }

        #endregion

        #region 工具

        /// <summary>把规则表里的放大器 key 标出来，聚合时跳过对它们的放大。</summary>
        private static bool[] BuildAmplifierKeyMask()
        {
            var mask = new bool[KeyCount];
            IReadOnlyList<BuffAmplifyRule> rules = BuffRules.AmplifyRules;
            for (int i = 0; i < rules.Count; i++)
            {
                int index = (int)rules[i].AmplifierKey;
                if (index >= 0 && index < KeyCount)
                {
                    mask[index] = true;
                }
            }

            return mask;
        }

        private static int IndexOf(BuffSourceKind source)
        {
            for (int i = 0; i < SourceKinds.Length; i++)
            {
                if (SourceKinds[i] == source)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool SameContent(List<BuffModifier> current, IEnumerable<BuffModifier> next)
        {
            if (next == null)
            {
                return current.Count == 0;
            }

            if (next is IReadOnlyList<BuffModifier> list)
            {
                if (list.Count != current.Count)
                {
                    return false;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i].Equals(current[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // 一次性枚举（LINQ 结果等）：先落成列表再比
            var buffer = new List<BuffModifier>(next);
            if (buffer.Count != current.Count)
            {
                return false;
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                if (!buffer[i].Equals(current[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddWarning(string message)
        {
            // 重建时每条错配置只报一次，不刷屏
            if (!_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        private static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;

        #endregion
    }
}
