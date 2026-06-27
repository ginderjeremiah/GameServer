using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for skill-synthesis recipes and their related collections (input skills and
    /// proficiency-level conditions). Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminSkillRecipes
    {
        /// <summary>Applies an identity-level Add/Edit change set to the recipe catalogue (retire-only — a Delete
        /// is rejected). Fails (applying nothing) if an edit targets a recipe that does not exist, or a result
        /// skill that does not exist, is retired, or is not Synthesis-flagged, or if the change would make the
        /// recipe graph cyclic.</summary>
        AdminSaveResult SaveSkillRecipes(IReadOnlyList<Change<SkillRecipe>> changes);

        /// <summary>Reconciles a recipe's input skills. Fails if the recipe does not exist, the set is empty, an
        /// input skill does not exist or is retired, an input is the recipe's own result, or the inputs would
        /// make the recipe graph cyclic.</summary>
        AdminSaveResult SetInputs(SetSkillRecipeInputsData data);

        /// <summary>Reconciles a recipe's proficiency-level conditions. Fails if the recipe does not exist, a
        /// condition proficiency does not exist, or a condition level is out of range.</summary>
        AdminSaveResult SetConditions(SetSkillRecipeConditionsData data);
    }
}
