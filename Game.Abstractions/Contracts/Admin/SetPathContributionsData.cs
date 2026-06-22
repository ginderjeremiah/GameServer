namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of the skills that contribute to a path (with home tiers and weights), keyed by
    /// the owner path's <see cref="Id"/>. Reconciled against the existing contribution rows.
    /// </summary>
    public class SetPathContributionsData
    {
        public int Id { get; set; }
        public required List<SkillPathContribution> Contributions { get; set; }
    }
}
