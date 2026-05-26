namespace Game.Abstractions.Entities
{
    public partial class ZoneEnemy
    {
        public int ZoneId { get; set; }
        public int EnemyId { get; set; }
        public int Weight { get; set; }

        public virtual Zone Zone { get => field ?? throw new NavigationNotLoadedException(nameof(Zone)); set; }
        public virtual Enemy Enemy { get => field ?? throw new NavigationNotLoadedException(nameof(Enemy)); set; }
    }
}
