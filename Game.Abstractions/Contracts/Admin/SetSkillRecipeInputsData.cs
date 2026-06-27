namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a recipe's input skill ids, keyed by the owner's <see cref="Id"/>. Reconciled
    /// against the existing inputs. A recipe must have at least one input.
    /// </summary>
    public class SetSkillRecipeInputsData
    {
        public int Id { get; set; }
        public required List<int> SkillIds { get; set; }
    }
}
