using System;
using System.Collections.Generic;
using TalentTree.Effects;
using UnityEngine;

namespace TalentTree
{
    /// <summary>
    /// 复杂一点的天赋树，可能每一个等级都会要求上一个天赋达到一定等级；可能每一个等级都会有上一个等级没有的字段效果，所以我将Requirement与Effect写到了LevelData当中
    /// </summary>
    [Serializable]
    public sealed class TalentLevelData
    {
        [Tooltip("当前天赋消耗的点数")]
        [SerializeField, Min(0)] private int _talentPointCost;
        [Tooltip("当前天赋的前置依赖")]
        [SerializeReference] private List<TalentRequirement> _requirements = new();
        [Tooltip("当前天赋的效果")]
        [SerializeReference] private List<TalentEffect> _effects = new();

        public int TalentPointCost => Mathf.Max(0, _talentPointCost);
        public IReadOnlyList<TalentRequirement> Requirements => _requirements;
        public IReadOnlyList<TalentEffect> Effects => _effects;

        public List<TalentRequirement> MutableRequirements => _requirements ??= new List<TalentRequirement>();
        public List<TalentEffect> MutableEffects => _effects ??= new List<TalentEffect>();

        public void SetTalentPointCost(int cost) => _talentPointCost = Mathf.Max(0, cost);
    }
}
