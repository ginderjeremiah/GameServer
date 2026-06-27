using CoreSkillRecipe = Game.Core.Skills.SkillRecipe;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Read access to the cached skill-synthesis recipe catalogue (spike #1125): the contract projection the
    /// client and admin Workbench read, the lean domain model the synthesis command validates against, and the
    /// input-skill → recipe-ids reverse index that drives the client's hinted reveal.
    /// </summary>
    public interface ISkillRecipes
    {
        public List<Contracts.SkillRecipe> AllSkillRecipes();

        /// <summary>Whether <paramref name="recipeId"/> resolves to a recipe in the catalogue — the anti-cheat
        /// bounds guard the synthesis command runs before the indexed <see cref="GetSkillRecipe"/>, so a
        /// tampered out-of-range id is rejected cleanly instead of throwing. Retired recipes still resolve
        /// (retirement is checked downstream, not here).</summary>
        public bool ValidateRecipeId(int recipeId);

        public CoreSkillRecipe GetSkillRecipe(int recipeId);

        /// <summary>The (non-retired) recipes the given skill is an input to, or empty if none — the reverse
        /// index the client hint state consumes when the player owns the skill.</summary>
        public IReadOnlyList<int> RecipesForInputSkill(int skillId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
