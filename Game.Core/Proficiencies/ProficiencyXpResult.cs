namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A single proficiency's outcome from one won battle's XP accrual: how much XP it gained, the level and
    /// residual XP it ended at, the authored milestone levels the gain crossed, and the reward skills those
    /// milestones granted. Aggregated per battle and pushed to the client (spike #982 decision 9) so a level-up
    /// or milestone surfaces immediately rather than only on the next reload. A bonus-only milestone is one in
    /// <see cref="MilestonesCrossed"/> with no matching entry in <see cref="GrantedSkillIds"/>.
    /// </summary>
    public record ProficiencyXpResult(
        int ProficiencyId,
        decimal XpGained,
        int NewLevel,
        decimal NewXp,
        IReadOnlyList<int> MilestonesCrossed,
        IReadOnlyList<int> GrantedSkillIds);
}
