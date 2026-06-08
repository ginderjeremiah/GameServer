namespace Game.Infrastructure.Entities
{
    public class AttributeDistribution
    {
        public int EnemyId { get; set; }
        public int AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public virtual Enemy Enemy { get => field ?? throw new NotLoadedException(nameof(Enemy)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
