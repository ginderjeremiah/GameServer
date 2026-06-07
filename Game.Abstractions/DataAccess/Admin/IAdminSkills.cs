using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for skills and their damage multipliers. Encapsulates the EF
    /// specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminSkills
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the skill catalogue.</summary>
        void SaveSkills(IReadOnlyList<Change<Skill>> changes);

        /// <summary>Applies a change set to a skill's damage multipliers. Returns <c>false</c> if the skill does not exist.</summary>
        bool SetMultipliers(AddEditAttributesData data);
    }
}
