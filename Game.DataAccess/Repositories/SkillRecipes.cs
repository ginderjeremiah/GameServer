using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreSkillRecipe = Game.Core.Skills.SkillRecipe;

namespace Game.DataAccess.Repositories
{
    internal class SkillRecipes(SkillRecipesCacheHolder holder) : ISkillRecipes, ISkillRecipeEntityCache
    {
        // Read the immutable snapshot once per logical operation (docs/backend.md → Reference-data snapshot
        // read-once idiom) so a build-then-swap between reads can't mix an old and a new snapshot in one call.
        private SkillRecipeSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.SkillRecipe> AllSkillRecipes()
        {
            return [.. Snapshot.Entities.Select(SkillRecipeMapper.ToContract)];
        }

        public CoreSkillRecipe GetSkillRecipe(int recipeId)
        {
            // Returns the snapshot's shared, pre-materialized immutable instance rather than re-mapping.
            return Snapshot.CoreRecipes.GetById(recipeId, "skill recipe");
        }

        public IReadOnlyList<int> RecipesForInputSkill(int skillId)
        {
            return Snapshot.RecipeIdsByInputSkill.TryGetValue(skillId, out var recipeIds)
                ? recipeIds
                : [];
        }

        public SkillRecipe? LookupSkillRecipe(int recipeId)
        {
            return Snapshot.Entities.Lookup(recipeId);
        }

        public IReadOnlyList<SkillRecipe> AllSkillRecipeEntities()
        {
            return Snapshot.Entities;
        }
    }
}
