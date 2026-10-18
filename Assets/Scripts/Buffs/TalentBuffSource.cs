using System.Collections.Generic;
using TalentTree.Effects;

namespace TapTapGameJam.Buffs
{
    /// <summary>
    /// 把科技树天赋翻译成 Buff。<see cref="BuffSourceKind.Talent"/> 的唯一数据来源。
    ///
    /// 翻译规则只有三条：
    /// <list type="bullet">
    /// <item>Tag 类 key（<see cref="BuffKind.Flag"/>）→ <see cref="BuffOp.Flag"/>，数值丢弃；</item>
    /// <item>计数类 key（<see cref="BuffKind.Flat"/>）→ <see cref="BuffOp.Flat"/>，数值原样；</item>
    /// <item>其余（Grow / Shrink）→ <see cref="BuffOp.Percent"/>，数值原样。</item>
    /// </list>
    /// 也就是说<b>配表只填一个数，是百分比还是固定值由 key 自己决定</b>，策划不用选 op，也就不会选错。
    ///
    /// 解析不了的 key 不会被静默丢掉，而是进 <see cref="UnknownKeys"/>，由宿主（PlayerBuffs）打到 Console。
    /// 纯 C#，不依赖 UnityEngine，可被 Tools/BuffChecks 直接测。
    /// </summary>
    public sealed class TalentBuffSource
    {
        private readonly TalentEffectResolver _resolver;
        private readonly List<BuffModifier> _buffer = new List<BuffModifier>();
        private readonly List<string> _unknownKeys = new List<string>();

        public TalentBuffSource(TalentEffectResolver resolver) => _resolver = resolver;

        /// <summary>
        /// 上一次 <see cref="Build"/> 里解析失败的 key（去重后）。都是「资产里写了但代码里没有」的拼写错误。
        /// </summary>
        public IReadOnlyList<string> UnknownKeys => _unknownKeys;

        /// <summary>
        /// 生成当前所有天赋效果的 modifier 列表。返回的列表会被下一次调用复用，调用方不要持有。
        ///
        /// 顺序做了排序：同样的天赋进度一定产出同样的序列，这样
        /// <see cref="BuffAggregator.SetSource"/> 的内容比对才有意义（否则每次重算都会误判成「变了」）。
        /// </summary>
        public IReadOnlyList<BuffModifier> Build()
        {
            _buffer.Clear();
            _unknownKeys.Clear();

            IReadOnlyList<TalentEffect> effects = _resolver?.GetActiveEffects();
            if (effects == null)
            {
                return _buffer;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                TalentEffect effect = effects[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.EffectKey))
                {
                    continue;
                }

                if (!BuffKeys.TryParse(effect.EffectKey, out BuffKey key))
                {
                    if (!_unknownKeys.Contains(effect.EffectKey))
                    {
                        _unknownKeys.Add(effect.EffectKey);
                    }

                    continue;
                }

                _buffer.Add(BuffModifier.FromValue(key, effect.NumericValue, BuffSourceKind.Talent));
            }

            _buffer.Sort(BuffModifier.CompareStable);
            return _buffer;
        }
    }
}
