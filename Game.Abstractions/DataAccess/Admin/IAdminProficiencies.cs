using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for proficiencies and their related collections (path membership,
    /// per-level bonuses, per-level reward skills, and cross-path prerequisite edges). Skill contributions
    /// now belong to the owning path — see <see cref="IAdminPaths"/>. Encapsulates the EF specifics behind
    /// an entity-free admin contract surface.
    /// </summary>
    public interface IAdminProficiencies
    {
        /// <summary>Applies an identity-level Add/Edit change set to the proficiency catalogue (retire-only —
        /// a Delete is rejected). Fails (applying nothing) if an edit targets a proficiency that does not
        /// exist, a path that does not exist, or a seed skill that is not Player-acquirable.</summary>
        AdminSaveResult SaveProficiencies(IReadOnlyList<Change<Proficiency>> changes);

        /// <summary>Reconciles a proficiency's per-level attribute bonuses. Fails if the proficiency does not exist.</summary>
        AdminSaveResult SetModifiers(SetProficiencyModifiersData data);

        /// <summary>Reconciles a proficiency's per-level reward skills. Fails if the proficiency does not
        /// exist or a reward skill is not Player-acquirable.</summary>
        AdminSaveResult SetRewards(SetProficiencyRewardsData data);

        /// <summary>Reconciles the cross-path prerequisite edges of every proficiency named in
        /// <paramref name="changes"/> as one batch. Validated against the combined prospective graph — every
        /// changed proficiency's desired edges plus every other proficiency's existing ones — so a gateway
        /// swap spanning several proficiencies (one drops an edge while another gains the reverse) is judged
        /// by its final acyclic state rather than rejected as a false cycle depending on submission order.
        /// Fails (applying nothing) if a proficiency does not exist, a prerequisite does not exist, a
        /// proficiency lists itself, a proficiency is named more than once in the batch, or the combined
        /// result would cycle.</summary>
        AdminSaveResult SetPrerequisites(IReadOnlyList<SetProficiencyPrerequisitesData> changes);
    }
}
