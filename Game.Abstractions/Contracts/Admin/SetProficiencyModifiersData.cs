namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a proficiency's per-level attribute bonuses, keyed by the owner's
    /// <see cref="Id"/>. Reconciled against the existing rows (delete/insert/update the difference).
    /// </summary>
    public class SetProficiencyModifiersData
    {
        public int Id { get; set; }
        public required List<ProficiencyLevelModifier> Modifiers { get; set; }
    }
}
