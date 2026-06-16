using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for skills, their damage multipliers, and their effects.
    /// Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminSkills
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the skill catalogue.
        /// Fails (applying nothing) if an edit targets a skill that does not exist.</summary>
        AdminSaveResult SaveSkills(IReadOnlyList<Change<Skill>> changes);

        /// <summary>Applies a change set to a skill's damage multipliers. Fails if the skill does not exist.</summary>
        AdminSaveResult SetMultipliers(AddEditAttributesData data);

        /// <summary>Applies a change set to a skill's effects. Fails if the skill does not exist.</summary>
        AdminSaveResult SetEffects(SetSkillEffectsData data);
    }
}
