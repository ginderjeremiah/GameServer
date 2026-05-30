namespace Game.Api.Models.Zones
{
    public class SetZoneEnemiesData
    {
        public int ZoneId { get; set; }

        public required List<ZoneEnemy> ZoneEnemies { get; set; }
    }
}
