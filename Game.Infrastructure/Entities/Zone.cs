namespace Game.Infrastructure.Entities
{
    public class Zone : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }

        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
