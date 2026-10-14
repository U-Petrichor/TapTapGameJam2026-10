using TapTapGameJam.Events;

namespace TalentTree
{
    public readonly struct TalentLevelChangedEvent : IEvent
    {
        public TalentLevelChangedEvent(TalentData talent, int oldLevel, int newLevel)
        {
            Talent = talent;
            OldLevel = oldLevel;
            NewLevel = newLevel;
        }

        public TalentData Talent { get; }
        public int OldLevel { get; }
        public int NewLevel { get; }
    }
}
