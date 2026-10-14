using System.Collections.Generic;

namespace TalentTree
{
    /// <summary>
    /// 解析玩家当前等级下有效的效果。
    /// 同一天赋的同一 EffectKey 只取达到的最高等级；不同天赋之间保留并叠加。
    /// </summary>
    public sealed class TalentEffectResolver
    {
        private readonly PlayerTalentTree _tree;

        public TalentEffectResolver(PlayerTalentTree tree) => _tree = tree;

        public IReadOnlyList<TalentEffect> GetActiveEffects(string effectKey = null)
        {
            var selected = new Dictionary<(TalentData Talent, string Key), TalentEffect>();
            if (_tree == null || _tree.TalentTreeAsset == null) return new List<TalentEffect>();

            foreach (TalentNodeData node in _tree.TalentTreeAsset.Nodes)
            {
                TalentData talent = node?.Data;
                if (talent == null) continue;

                int currentLevel = _tree.GetLevel(talent);
                for (int level = 1; level <= currentLevel; level++)
                {
                    TalentLevelData levelData = talent.GetLevelData(level);
                    if (levelData?.Effects == null) continue;
                    foreach (TalentEffect effect in levelData.Effects)
                    {
                        if (effect == null || string.IsNullOrWhiteSpace(effect.EffectKey)) continue;
                        if (!string.IsNullOrWhiteSpace(effectKey) && effect.EffectKey != effectKey) continue;
                        selected[(talent, effect.EffectKey)] = effect;
                    }
                }
            }

            return new List<TalentEffect>(selected.Values);
        }

        public float GetNumericTotal(string effectKey)
        {
            float total = 0f;
            foreach (TalentEffect effect in GetActiveEffects(effectKey)) total += effect.NumericValue;
            return total;
        }
    }
}
