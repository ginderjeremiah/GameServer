namespace Game.Infrastructure.Entities
{
    public class ZoneEnemy
    {
        public int ZoneId { get; set; }
        public int EnemyId { get; set; }
        public int Weight { get; set; }

        public virtual Zone Zone { get => field ?? throw new NotLoadedException(nameof(Zone)); set; }
        public virtual Enemy Enemy { get => field ?? throw new NotLoadedException(nameof(Enemy)); set; }
    }
}
