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
        /// Fails (applying nothing) if an edit targets an enemy that does not exist.</summary>
        AdminSaveResult SaveEnemies(IReadOnlyList<Change<Enemy>> changes);

        /// <summary>Replaces an enemy's attribute distributions. Fails if the enemy does not exist.</summary>
        AdminSaveResult SetAttributeDistributions(SetEnemyAttributeDistributions data);

        /// <summary>Replaces an enemy's skill pool. Fails if the enemy does not exist.</summary>
        AdminSaveResult SetSkills(SetEnemySkillsData data);

        /// <summary>Replaces an enemy's zone spawns. Fails if the enemy does not exist.</summary>
        AdminSaveResult SetSpawns(SetEnemySpawnsData data);
    }
}
