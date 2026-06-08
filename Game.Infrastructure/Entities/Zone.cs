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

        /// <summary>The zone's single dedicated boss, fought via the "Challenge Boss" action. Null when no
        /// boss has been authored. Distinct from the random <see cref="ZoneEnemies"/> spawn table.</summary>
        public int? BossEnemyId { get; set; }

        /// <summary>The fixed level the dedicated boss is fought at, independent of <see cref="LevelMin"/>/
        /// <see cref="LevelMax"/>. Only meaningful when <see cref="BossEnemyId"/> is set.</summary>
        public int BossLevel { get; set; }

        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
