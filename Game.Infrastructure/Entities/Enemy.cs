namespace Game.Infrastructure.Entities
{
    public class Enemy : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsBoss { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get => field ?? throw new NotLoadedException(nameof(AttributeDistributions)); set; }
        public virtual List<EnemySkill> EnemySkills { get => field ?? throw new NotLoadedException(nameof(EnemySkills)); set; }
        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
