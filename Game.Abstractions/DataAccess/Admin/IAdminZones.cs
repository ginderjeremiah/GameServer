using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for zones and their enemy spawn assignments. Encapsulates the EF
    /// specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminZones
    {
        /// <summary>
        /// Applies an identity-level Add/Edit/Delete change set to the zone catalogue. Fails (applying
        /// nothing) with a user-facing message when an edit targets a zone that does not exist, or an
        /// Add/Edit sets a <see cref="Zone.BossEnemyId"/> that does not reference an existing boss enemy,
        /// or an <see cref="Zone.UnlockChallengeId"/> that is out of range.
        /// </summary>
        AdminSaveResult SaveZones(IReadOnlyList<Change<Zone>> changes);

        /// <summary>Replaces a zone's enemy spawns. Fails if the zone does not exist.</summary>
        AdminSaveResult SetEnemies(SetZoneEnemiesData data);
    }
}
