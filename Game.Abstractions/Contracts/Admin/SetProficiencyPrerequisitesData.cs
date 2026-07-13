namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a proficiency's prerequisite proficiency ids, keyed by the owner's
    /// <see cref="Id"/>. Reconciled against the existing edges. Submitted as a batch (one entry per
    /// changed proficiency) so a gateway swap spanning several proficiencies is validated against its
    /// final, combined graph rather than proficiency-by-proficiency — see
    /// <see cref="Game.Abstractions.DataAccess.Admin.IAdminProficiencies.SetPrerequisites"/>.
    /// </summary>
    public class SetProficiencyPrerequisitesData
    {
        public int Id { get; set; }
        public required List<int> PrerequisiteIds { get; set; }
    }
}
