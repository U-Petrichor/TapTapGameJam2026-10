using System.Collections.Generic;

namespace TalentTree.Effects
{
    /// <summary>
    /// 解析玩家当前等级下有效的效果。
    /// 同一天赋的同一 EffectKey 只取达到的最高等级；不同天赋之间保留并叠加。
    ///
    /// <b>语义约定（配表时必须遵守）</b>：每一级填的数值是「到这一级为止的<b>累计总量</b>」，不是增量。
    /// 因为同一天赋同一 key 只取最高档，填成增量的话 3 级只会生效第 3 级那一份，1、2 级被丢掉。
    ///
    /// <b>缓存</b>：完整结果只在第一次读的时候算一次（<see cref="Build"/>）。
    /// 天赋等级变化后必须调 <see cref="Invalidate"/>，否则读到的是旧结果。
    /// 这里不订阅事件，因为订阅是宿主（PlayerTalentTree / PlayerBuffs）的责任——
    /// resolver 是纯 C#，不应该依赖 Unity 的事件总线。
    /// </summary>
    public sealed class TalentEffectResolver
    {
        private readonly ITalentLevelSource _source;

        /// <summary>上一次算出来的结果。null 表示缓存失效。</summary>
        private List<TalentEffect> _active;

        public TalentEffectResolver(ITalentLevelSource source) => _source = source;

        /// <summary>
        /// 当前所有生效的效果（不可变视图，调用方不要改）。
        /// 直接返回缓存列表的引用，所以<b>不要在遍历时调 <see cref="Invalidate"/></b>。
        /// </summary>
        public IReadOnlyList<TalentEffect> GetActiveEffects()
        {
            return _active ??= Build();
        }

        /// <summary>按 key 过滤。会新分配一个列表，只适合低频调用（UI、排查）。</summary>
        public IReadOnlyList<TalentEffect> GetActiveEffects(string effectKey)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
            {
                return GetActiveEffects();
            }

            var filtered = new List<TalentEffect>();
            IReadOnlyList<TalentEffect> all = GetActiveEffects();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].EffectKey == effectKey)
                {
                    filtered.Add(all[i]);
                }
            }

            return filtered;
        }

        /// <summary>
        /// 某个 key 上所有生效效果的数值之和（加法求和）。
        /// 注意：这是「纯求和」，不含乘法叠加、不含夹取——Buff 层统一处理那部分，
        /// 这里保留只是为了还没迁移过来的老调用点。
        /// </summary>
        public float GetNumericTotal(string effectKey)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
            {
                return 0f;
            }

            float total = 0f;
            IReadOnlyList<TalentEffect> all = GetActiveEffects();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].EffectKey == effectKey)
                {
                    total += all[i].NumericValue;
                }
            }

            return total;
        }

        /// <summary>等级变化后调用。下一次读会重新算。</summary>
        public void Invalidate() => _active = null;

        /// <summary>
        /// 展开：遍历整棵树 → 每个天赋取 1..当前等级的所有档位 → (天赋, key) 为键覆盖写。
        /// 覆盖写带来的效果就是「同一天赋同一 key 只留最高等级那一档」。
        /// </summary>
        private List<TalentEffect> Build()
        {
            var selected = new Dictionary<(TalentData Talent, string Key), TalentEffect>();
            TalentTreeAsset asset = _source?.Asset;

            if (asset != null)
            {
                IReadOnlyList<TalentNodeData> nodes = asset.Nodes;
                for (int n = 0; n < nodes.Count; n++)
                {
                    TalentData talent = nodes[n]?.Data;
                    if (talent == null)
                    {
                        continue;
                    }

                    int currentLevel = _source.GetLevel(talent);
                    for (int level = 1; level <= currentLevel; level++)
                    {
                        TalentLevelData levelData = talent.GetLevelData(level);
                        if (levelData?.Effects == null)
                        {
                            continue;
                        }

                        IReadOnlyList<TalentEffect> effects = levelData.Effects;
                        for (int e = 0; e < effects.Count; e++)
                        {
                            TalentEffect effect = effects[e];
                            if (effect == null || string.IsNullOrWhiteSpace(effect.EffectKey))
                            {
                                continue;
                            }

                            selected[(talent, effect.EffectKey)] = effect;
                        }
                    }
                }
            }

            return new List<TalentEffect>(selected.Values);
        }
    }
}
