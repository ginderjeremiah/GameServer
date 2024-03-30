namespace DataAccess.Models.Attributes
{
    public class AttributeDistribution
    {
        public int EnemyId { get; set; }
        public int AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }
    }
}
