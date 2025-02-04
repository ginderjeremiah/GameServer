namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the data needed to initialize a battle.
    /// </summary>
    public class BattleData
    {
        /// <summary>
        /// The unique identifier of the enemy.
        /// </summary>
        public int EnemyId { get; set; }

        /// <summary>
        /// The level of the enemy.
        /// </summary>
        public int EnemyLevel { get; set; }

        /// <summary>
        /// The time that the battle started.
        /// </summary>
        public DateTime BattleStartTime { get; set; }

        /// <summary>
        /// The seed for battle rng.
        /// </summary>
        public uint Seed { get; set; }

        /// <summary>
        /// Generates a hash for the data.
        /// </summary>
        /// <returns></returns>
        public string Hash()
        {
            var data = $"{EnemyId}|{EnemyLevel}|{BattleStartTime}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
