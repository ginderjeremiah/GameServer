namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a recipe's proficiency-level conditions, keyed by the owner's <see cref="Id"/>.
    /// Reconciled against the existing conditions.
    /// </summary>
    public class SetSkillRecipeConditionsData
    {
        public int Id { get; set; }
        public required List<SkillRecipeCondition> Conditions { get; set; }
    }
}
