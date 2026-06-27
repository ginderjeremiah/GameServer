namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// An input edge of a <see cref="SkillRecipe"/>: the recipe requires the player to own
    /// <see cref="SkillId"/> (as an unlocked skill) to be synthesized. Keyed by (recipe, skill).
    /// </summary>
    public class SkillRecipeInput
    {
        public int RecipeId { get; set; }
        public int SkillId { get; set; }

        public virtual SkillRecipe Recipe { get => field ?? throw new NotLoadedException(nameof(Recipe)); set; }
        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
    }
}
