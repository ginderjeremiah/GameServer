namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a proficiency's per-level reward skills (milestone payouts), keyed by the
    /// owner's <see cref="Id"/>. Reconciled against the existing rows.
    /// </summary>
    public class SetProficiencyRewardsData
    {
        public int Id { get; set; }
        public required List<ProficiencyLevelReward> Rewards { get; set; }
    }
}
