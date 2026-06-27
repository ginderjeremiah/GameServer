using SkillRecipeEntity = Game.Infrastructure.Entities.SkillRecipe;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached skill-recipe <em>entities</em> for the Content Authoring admin persistence,
    /// which needs the EF entities for existence/diff lookups and to build the prospective dependency graph for
    /// the acyclicity guard. Kept out of the public <see cref="Abstractions.DataAccess.ISkillRecipes"/> read
    /// contract, which returns recipe contracts — the entity is an implementation detail of this layer.
    /// </summary>
    internal interface ISkillRecipeEntityCache
    {
        /// <summary>The cached recipe entity at <paramref name="recipeId"/> (its zero-based index), or null if out of range.</summary>
        SkillRecipeEntity? LookupSkillRecipe(int recipeId);

        /// <summary>Every cached recipe entity (with its inputs and conditions loaded), for the acyclicity graph.</summary>
        IReadOnlyList<SkillRecipeEntity> AllSkillRecipeEntities();
    }
}
