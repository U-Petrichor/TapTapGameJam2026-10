namespace TalentTree
{
    public static class TalentRequirementEvaluator
    {
        public static bool AreMet(TalentLevelData levelData, PlayerTalentTree tree, out string failureReason)
        {
            failureReason = string.Empty;
            if (levelData == null) return false;

            if (levelData.Requirements == null) return true;
            TalentRequirementContext context = new(tree);
            foreach (TalentRequirement requirement in levelData.Requirements)
            {
                if (requirement == null || requirement.IsMet(context)) continue;
                failureReason = requirement.FailureReason;
                return false;
            }

            return true;
        }
    }
}
