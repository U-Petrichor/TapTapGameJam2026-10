namespace TapTapGameJam.MiniGame
{
    /// <summary>
    /// Describes a prerequisite that must be satisfied before a task point can
    /// be selected for a newly generated task. The object owns its state; the
    /// caller only reads the result.
    /// </summary>
    public interface ITaskSpawnCondition
    {
        bool IsAvailable { get; }
        string UnavailableReason { get; }
    }
}
