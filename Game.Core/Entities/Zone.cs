namespace Game.Core.Entities
{
    public partial class Zone
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }

        public virtual List<ZoneDrop> ZoneDrops { get; set; }
        public virtual List<ZoneEnemy> ZoneEnemies { get; set; }
    }
}
