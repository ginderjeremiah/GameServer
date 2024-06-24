namespace GameCore.Entities
{
    public class Enemy
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get; set; }
        public virtual List<EnemyDrop> EnemyDrops { get; set; }
        public virtual List<EnemySkill> EnemySkills { get; set; }
        public virtual List<ZoneEnemy> ZoneEnemies { get; set; }
    }
}
