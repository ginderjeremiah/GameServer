using Game.Core.Enemies;
using Game.Core.Zones;

namespace Game.Core.Battle
{
    /// <summary>
    /// Builds the enemy encounter for a new battle. Choosing the enemy's level (within a zone's
    /// configured range, or the zone's fixed boss level) and selecting its battle skills are
    /// game-design decisions, so they live in the domain rather than in the orchestration layer.
    /// </summary>
    public class BattleFactory
    {
        /// <summary>
        /// Creates a battle-ready enemy for a random idle encounter in <paramref name="zone"/>: rolls the
        /// encounter level within the zone's range, resolves the enemy to fight at that level via
        /// <paramref name="resolveEnemy"/>, and randomly selects its battle skills. The chosen loadout lives
        /// on the returned enemy (<see cref="Enemy.BattleSkills"/>) for the caller to snapshot.
        /// </summary>
        /// <param name="resolveEnemy">
        /// Supplies the enemy for the rolled level. The caller provides this so the domain stays
        /// independent of the data-access layer that owns enemy selection.
        /// </param>
        public Enemy CreateBattleEnemy(Zone zone, Func<int, Enemy> resolveEnemy)
        {
            var level = zone.RollEncounterLevel();
            var enemy = resolveEnemy(level);
            enemy.SelectBattleSkills();
            return enemy;
        }

        /// <summary>
        /// Creates the battle-ready dedicated boss for <paramref name="zone"/>'s "Challenge Boss" action.
        /// Unlike <see cref="CreateBattleEnemy"/> this is fully deterministic: the boss is resolved at the
        /// zone's fixed <see cref="Zone.BossLevel"/> (no random roll) and brings its full authored loadout
        /// (<see cref="Enemy.SelectAllBattleSkills"/>, no random selection), which is exactly the determinism
        /// frontend/backend battle parity wants. The caller resolves the boss enemy (by
        /// <see cref="Zone.BossEnemyId"/>) through <paramref name="resolveEnemy"/>, keeping the domain free
        /// of data-access references.
        /// </summary>
        public Enemy CreateBossEnemy(Zone zone, Func<int, Enemy> resolveEnemy)
        {
            var enemy = resolveEnemy(zone.BossLevel);
            enemy.SelectAllBattleSkills();
            return enemy;
        }
    }
}
