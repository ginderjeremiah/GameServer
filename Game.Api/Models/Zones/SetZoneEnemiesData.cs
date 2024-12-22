namespace Game.Api.Models.Zones
{
    public class SetZoneEnemiesData
    {
        public int ZoneId { get; set; }

        public List<ZoneEnemy> ZoneEnemies { get; set; }
    }
}
