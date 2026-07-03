using CoreSkillRecipe = Game.Core.Skills.SkillRecipe;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Read access to the cached skill-synthesis recipe catalogue (spike #1125): the contract projection the
    /// client and admin Workbench read, and the lean domain model the synthesis command validates against. The
    /// client derives its own hinted-reveal state from the full recipe set (<see cref="AllSkillRecipes"/>) plus
    /// owned skills/proficiencies rather than a server-computed reverse index.
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

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
