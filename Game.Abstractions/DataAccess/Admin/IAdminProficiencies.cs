using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for proficiencies and their related collections (per-level bonuses,
    /// per-level reward skills, prerequisite edges, and skill contributions). Encapsulates the EF specifics
    /// behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminProficiencies
    {
        /// <summary>Applies an identity-level Add/Edit change set to the proficiency catalogue (retire-only —
        /// a Delete is rejected). Fails (applying nothing) if an edit targets a proficiency that does not
        /// exist, or a seed skill that is not Player-acquirable.</summary>
        AdminSaveResult SaveProficiencies(IReadOnlyList<Change<Proficiency>> changes);

        /// <summary>Reconciles a proficiency's per-level attribute bonuses. Fails if the proficiency does not exist.</summary>
        AdminSaveResult SetModifiers(SetProficiencyModifiersData data);

        /// <summary>Reconciles a proficiency's per-level reward skills. Fails if the proficiency does not
        /// exist or a reward skill is not Player-acquirable.</summary>
        AdminSaveResult SetRewards(SetProficiencyRewardsData data);

        /// <summary>Reconciles a proficiency's prerequisite edges. Fails if the proficiency does not exist,
        /// a prerequisite does not exist, or a proficiency lists itself.</summary>
        AdminSaveResult SetPrerequisites(SetProficiencyPrerequisitesData data);

        /// <summary>Reconciles the skills that contribute to a proficiency. Fails if the proficiency or a
        /// contributing skill does not exist.</summary>
        AdminSaveResult SetContributions(SetProficiencyContributionsData data);
    }
}
