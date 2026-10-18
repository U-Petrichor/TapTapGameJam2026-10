using System.Collections.Generic;

// TalentBuffSource 只依赖 TalentEffectResolver 的两个成员（EffectKey / NumericValue），
// 所以这里给最小的替身，把真正的天赋解析留在 Unity 那边跑。
// 和 Tools/SortFileChecks/TestDoubles.cs 是同一个套路。
namespace TalentTree.Effects
{
    public class TalentEffect
    {
        private string _effectKey;
        private float _numericValue;

        public string EffectKey => _effectKey;
        public float NumericValue => _numericValue;

        public static TalentEffect Make(string effectKey, float numericValue)
            => new TalentEffect { _effectKey = effectKey, _numericValue = numericValue };
    }

    public class TalentEffectResolver
    {
        private readonly List<TalentEffect> _effects;

        public TalentEffectResolver(List<TalentEffect> effects) => _effects = effects;

        public IReadOnlyList<TalentEffect> GetActiveEffects() => _effects;
    }
}
