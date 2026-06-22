namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of the skills that contribute to a proficiency (with weights), keyed by the
    /// owner proficiency's <see cref="Id"/>. Reconciled against the existing contribution rows.
    /// </summary>
    public class SetProficiencyContributionsData
    {
        public int Id { get; set; }
        public required List<SkillProficiencyContribution> Contributions { get; set; }
    }
}
