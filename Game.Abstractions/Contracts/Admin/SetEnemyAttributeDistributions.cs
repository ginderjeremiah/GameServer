namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of attribute distributions to associate with a single enemy (<see cref="EnemyId"/>).</summary>
    public class SetEnemyAttributeDistributions
    {
        public int EnemyId { get; set; }

        public required List<AttributeDistribution> AttributeDistributions { get; set; }
    }
}
