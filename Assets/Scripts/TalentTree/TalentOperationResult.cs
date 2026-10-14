namespace TalentTree
{
    public enum TalentOperationStatus
    {
        Success,
        TalentNotFound,
        AlreadyMaxLevel,
        AlreadyUnlocked,
        NotUnlocked,
        InsufficientTalentPoints,
        GraphRuleNotMet,
        RequirementNotMet,
        RemovalBlockedByDependency,
        NothingToReset
    }

    public readonly struct TalentOperationResult
    {
        private TalentOperationResult(TalentOperationStatus status, string message, int oldLevel, int newLevel, int pointChange)
        {
            Status = status;
            Message = message;
            OldLevel = oldLevel;
            NewLevel = newLevel;
            PointChange = pointChange;
        }

        public TalentOperationStatus Status { get; }
        public string Message { get; }
        public int OldLevel { get; }
        public int NewLevel { get; }
        public int PointChange { get; }
        public bool Succeeded => Status == TalentOperationStatus.Success;

        public static TalentOperationResult Success(int oldLevel, int newLevel, int pointChange, string message = "")
            => new(TalentOperationStatus.Success, message, oldLevel, newLevel, pointChange);

        public static TalentOperationResult Fail(TalentOperationStatus status, string message)
            => new(status, message, 0, 0, 0);
    }
}
