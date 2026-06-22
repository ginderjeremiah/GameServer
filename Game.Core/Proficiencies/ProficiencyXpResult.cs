namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A single proficiency's outcome from one won battle's XP accrual: how much XP it gained, the level and
    /// residual XP it ended at, and any authored milestone levels the gain crossed. Aggregated per battle and
    /// pushed to the client (spike #982 decision 9) so a level-up or milestone surfaces immediately rather
    /// than only on the next reload; the milestone <em>effects</em> are applied by the milestone sub-issue.
    /// </summary>
    public record ProficiencyXpResult(
        int ProficiencyId,
        decimal XpGained,
        int NewLevel,
        decimal NewXp,
        IReadOnlyList<int> MilestonesCrossed);
}
