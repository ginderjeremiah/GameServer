namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of enemy spawns to associate with a single zone (<see cref="ZoneId"/>).</summary>
    public class SetZoneEnemiesData
    {
        public int ZoneId { get; set; }

        public required List<ZoneEnemy> ZoneEnemies { get; set; }
    }
}
