using Game.Core.Enemies;
using Game.Core.Probability;

namespace Game.Core.Zones
{
    /// <summary>
    /// Represents a zone in the game world that contains enemies.
    /// </summary>
    public class Zone
    {
        /// <summary>
        /// The name of the zone.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// A short description of the zone.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// The order in which the zone appears in the game world.
        /// </summary>
        public required int Order { get; set; }

        /// <summary>
        /// The minimum of level of enemies that can be found in the zone.
        /// </summary>
        public required int LevelMin { get; set; }

        /// <summary>
        /// The maximum level of enemies that can be found in the zone.
        /// </summary>
        public required int LevelMax { get; set; }

        /// <summary>
        /// The enemies that can be found in the zone.
        /// </summary>
        public ProbabilityTable<Enemy> EnemyTable { get; set; }

        /// <summary>
        /// The drops that can be found in the zone.
        /// </summary>
        public List<ZoneDrop> Drops { get; set; }
    }
}
