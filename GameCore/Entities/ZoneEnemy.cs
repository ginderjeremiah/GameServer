namespace GameCore.Entities
{
    public class ZoneEnemy
    {
        public int ZoneId { get; set; }
        public int EnemyId { get; set; }

        public virtual Zone Zone { get; set; }
        public virtual Enemy Enemy { get; set; }
        public virtual ZoneEnemyProbability ZoneEnemyProbability { get; set; }
        public virtual ZoneEnemyAlias ZoneEnemyAlias { get; set; }
    }
}
