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
        /// Applies an identity-level Add/Edit/Delete change set to the zone catalogue. Returns <c>null</c>
        /// on success, or a user-facing error message (applying nothing) when an Add/Edit sets a
        /// <see cref="Zone.BossEnemyId"/> that does not reference an existing boss enemy, or an
        /// <see cref="Zone.UnlockChallengeId"/> that is out of range.
        /// </summary>
        string? SaveZones(IReadOnlyList<Change<Zone>> changes);

        /// <summary>Replaces a zone's enemy spawns. Returns <c>false</c> if the zone does not exist.</summary>
        bool SetEnemies(SetZoneEnemiesData data);
    }
}
