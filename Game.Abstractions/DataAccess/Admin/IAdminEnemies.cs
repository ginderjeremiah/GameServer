using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for enemies and their related collections (attribute
    /// distributions, skill pools, zone spawns). Encapsulates the EF specifics — fresh,
    /// navigation-free entities, Delete→Edit→Add ordering, and zero-based-id update semantics —
    /// behind an entity-free admin contract surface so <c>Game.Api</c> never sees the persistence shape.
    /// </summary>
    public interface IAdminEnemies
    {
        /// <summary>Applies an identity-level Add/Edit/Delete change set to the enemy catalogue.
        /// Returns <c>false</c> (applying nothing) if an edit targets an enemy that does not exist.</summary>
        bool SaveEnemies(IReadOnlyList<Change<Enemy>> changes);

        /// <summary>Replaces an enemy's attribute distributions. Returns <c>false</c> if the enemy does not exist.</summary>
        bool SetAttributeDistributions(SetEnemyAttributeDistributions data);

        /// <summary>Replaces an enemy's skill pool. Returns <c>false</c> if the enemy does not exist.</summary>
        bool SetSkills(SetEnemySkillsData data);

        /// <summary>Replaces an enemy's zone spawns. Returns <c>false</c> if the enemy does not exist.</summary>
        bool SetSpawns(SetEnemySpawnsData data);
    }
}
