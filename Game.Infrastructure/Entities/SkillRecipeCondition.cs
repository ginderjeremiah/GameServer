namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A proficiency-level gate on a <see cref="SkillRecipe"/>: <see cref="ProficiencyId"/> must be at least
    /// <see cref="MinLevel"/> for the recipe to be synthesized. Keyed by (recipe, proficiency) — one gate per
    /// proficiency, with <see cref="MinLevel"/> the payload.
    /// </summary>
    public class SkillRecipeCondition
    {
        public int RecipeId { get; set; }
        public int ProficiencyId { get; set; }
        public int MinLevel { get; set; }

        public virtual SkillRecipe Recipe { get => field ?? throw new NotLoadedException(nameof(Recipe)); set; }
        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
    }
}
