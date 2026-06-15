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
        /// Returns <c>false</c> (applying nothing) if an edit targets a skill that does not exist.</summary>
        bool SaveSkills(IReadOnlyList<Change<Skill>> changes);

        /// <summary>Applies a change set to a skill's damage multipliers. Returns <c>false</c> if the skill does not exist.</summary>
        bool SetMultipliers(AddEditAttributesData data);

        /// <summary>Applies a change set to a skill's effects. Returns <c>false</c> if the skill does not exist.</summary>
        bool SetEffects(SetSkillEffectsData data);
    }
}
