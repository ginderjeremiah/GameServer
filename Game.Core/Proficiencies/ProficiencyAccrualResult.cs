namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The proficiency outcomes of an XP accrual: the per-proficiency XP <see cref="Results"/> and the nodes
    /// <see cref="Opened"/>. Returned by a single won battle's accrual — the live path raises these as its push,
    /// the offline batch folds them across the away window (<see cref="ProficiencyGainAccumulator"/>) onto the
    /// welcome-back summary (spike #982 decision 9). <see cref="ProficiencyGainAccumulator.Build"/> reuses the
    /// same shape for the folded window aggregate (XP summed, levels final, milestones/skills/opened unioned).
    /// </summary>
    public record ProficiencyAccrualResult(
        IReadOnlyList<ProficiencyXpResult> Results,
        IReadOnlyList<ProficiencyOpened> Opened)
    {
        /// <summary>An accrual that trained nothing and opened nothing.</summary>
        public static ProficiencyAccrualResult Empty { get; } = new([], []);
    }
}
