namespace Game.Core.Proficiencies
{
    /// <summary>
    /// One proficiency a skill feeds, with its contribution weight. Produced as the reverse index
    /// (skill → contributions) bundled in the proficiency cache snapshot, for the battle XP path that
    /// lands in a later sub-issue.
    /// </summary>
    public class SkillContribution
    {
        public required int ProficiencyId { get; init; }
        public required double Weight { get; init; }
    }
}
