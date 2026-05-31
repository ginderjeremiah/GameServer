using Game.Api.Models.Attributes;

namespace Game.Api.Models.Enemies
{
    public class SetEnemyAttributeDistributions
    {
        public int EnemyId { get; set; }

        public required List<AttributeDistribution> AttributeDistributions { get; set; }
    }
}
