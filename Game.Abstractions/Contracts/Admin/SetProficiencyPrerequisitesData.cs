namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a proficiency's prerequisite proficiency ids, keyed by the owner's
    /// <see cref="Id"/>. Reconciled against the existing edges.
    /// </summary>
    public class SetProficiencyPrerequisitesData
    {
        public int Id { get; set; }
        public required List<int> PrerequisiteIds { get; set; }
    }
}
