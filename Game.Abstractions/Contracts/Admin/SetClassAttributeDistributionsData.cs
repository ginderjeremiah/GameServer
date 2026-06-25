namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of attribute distributions to associate with a single class (<see cref="ClassId"/>).</summary>
    public class SetClassAttributeDistributionsData
    {
        public int ClassId { get; set; }

        public required List<AttributeDistribution> AttributeDistributions { get; set; }
    }
}
