using UnityEngine;

namespace TalentTree
{
    [System.Serializable]
    public sealed class TalentLevelRequirement : TalentRequirement
    {
        [SerializeField] private TalentData _talent;
        [SerializeField, Min(0)] private int _requiredLevel = 1;

        public TalentData Talent => _talent;
        public int RequiredLevel => Mathf.Max(0, _requiredLevel);

        public override bool IsMet(TalentRequirementContext context)
        {
            return _talent != null
                   && context.TalentTree != null
                   && context.TalentTree.GetLevel(_talent) >= RequiredLevel;
        }

        public override string FailureReason => _talent == null
            ? "缺少前置天赋引用"
            : $"需要「{_talent.DisplayName}」达到 {RequiredLevel} 级";

        public override bool References(TalentData talent, out int minimumLevel)
        {
            minimumLevel = RequiredLevel;
            return _talent == talent;
        }

        public void SetRequirement(TalentData talent, int requiredLevel)
        {
            _talent = talent;
            _requiredLevel = Mathf.Max(0, requiredLevel);
        }
    }
}
