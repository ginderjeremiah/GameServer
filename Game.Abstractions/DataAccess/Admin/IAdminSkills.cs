using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for skills, their damage portions, damage multipliers, and effects.
    /// Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminSkills
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the skill catalogue.
        /// Fails (applying nothing) if an edit targets a skill that does not exist.</summary>
        AdminSaveResult SaveSkills(IReadOnlyList<Change<Skill>> changes);

        /// <summary>Applies a change set to a skill's damage portions (keyed by leaf type). Fails if the skill
        /// does not exist or a portion's weight is not positive.</summary>
        AdminSaveResult SetPortions(SetSkillPortionsData data);

        /// <summary>Applies a change set to a skill's damage multipliers. Fails if the skill does not exist.</summary>
        AdminSaveResult SetMultipliers(AddEditAttributesData data);

        /// <summary>Applies a change set to a skill's effects. Fails if the skill does not exist.</summary>
        AdminSaveResult SetEffects(SetSkillEffectsData data);
    }
}
