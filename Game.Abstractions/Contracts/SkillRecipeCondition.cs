namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a recipe's proficiency-level gate: <see cref="ProficiencyId"/> must
    /// be at least <see cref="MinLevel"/> for the recipe to be synthesized.</summary>
    public class SkillRecipeCondition : IModel
    {
        public int ProficiencyId { get; set; }
        public int MinLevel { get; set; }
    }
}
