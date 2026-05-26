namespace Game.Abstractions.Entities
{
    public partial class Enemy : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get => field ?? throw new NavigationNotLoadedException(nameof(AttributeDistributions)); set; }
        public virtual List<EnemySkill> EnemySkills { get => field ?? throw new NavigationNotLoadedException(nameof(EnemySkills)); set; }
        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NavigationNotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
