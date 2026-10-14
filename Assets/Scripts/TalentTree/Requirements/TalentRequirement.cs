using System;

namespace TalentTree
{
    [Serializable]
    public abstract class TalentRequirement
    {
        public abstract bool IsMet(TalentRequirementContext context);
        public abstract string FailureReason { get; }

        /// <summary>用于阻止降级破坏其他天赋的已满足条件。</summary>
        public virtual bool References(TalentData talent, out int minimumLevel)
        {
            minimumLevel = 0;
            return false;
        }
    }

    public readonly struct TalentRequirementContext
    {
        public TalentRequirementContext(PlayerTalentTree talentTree)
        {
            TalentTree = talentTree;
        }

        public PlayerTalentTree TalentTree { get; }
    }
}
