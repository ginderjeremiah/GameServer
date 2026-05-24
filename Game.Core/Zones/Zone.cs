using Game.Core.Enemies;
using Game.Core.Probability;

namespace Game.Core.Zones
{
    /// <summary>
    /// Represents a zone in the game world that contains enemies.
    /// </summary>
    public class Zone
    {
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required int Order { get; set; }
        public required int LevelMin { get; set; }
        public required int LevelMax { get; set; }
        public ProbabilityTable<Enemy> EnemyTable { get; set; }
    }
}
