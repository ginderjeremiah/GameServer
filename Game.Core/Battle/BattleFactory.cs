using Game.Core.Enemies;

namespace Game.Core.Battle
{
    /// <summary>
    /// Builds the enemy encounter for a new battle. Choosing the enemy's level within a zone's
    /// configured range and selecting its battle skills are game-design decisions, so they live in
    /// the domain rather than in the orchestration layer.
    /// </summary>
    public class BattleFactory
    {
        /// <summary>
        /// Creates a battle-ready enemy for a zone whose level range is
        /// [<paramref name="levelMin"/>, <paramref name="levelMax"/>] (inclusive): rolls the encounter
        /// level, resolves the enemy to fight at that level via <paramref name="resolveEnemy"/>, and
        /// randomly selects its battle skills. The chosen loadout lives on the returned enemy
        /// (<see cref="Enemy.BattleSkills"/>) for the caller to snapshot.
        /// </summary>
        /// <param name="resolveEnemy">
        /// Supplies the enemy for the rolled level. The caller provides this so the domain stays
        /// independent of the data-access layer that owns enemy selection.
        /// </param>
        public Enemy CreateBattleEnemy(int levelMin, int levelMax, Func<int, Enemy> resolveEnemy)
        {
            var level = Random.Shared.Next(levelMin, levelMax + 1);
            var enemy = resolveEnemy(level);
            enemy.SelectBattleSkills();
            return enemy;
        }
    }
}
