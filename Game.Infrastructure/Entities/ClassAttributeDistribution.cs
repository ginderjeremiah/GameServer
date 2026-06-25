namespace Game.Infrastructure.Entities
{
    /// <summary>A <see cref="Class"/>'s per-attribute base/per-level distribution — the level-scaled, locked
    /// attribute fingerprint (<c>BaseAmount + AmountPerLevel × level</c>), the same shape as an enemy's
    /// <see cref="AttributeDistribution"/>. Assembled into the battler in a later sub-issue (#1223).</summary>
    public class ClassAttributeDistribution
    {
        public int ClassId { get; set; }
        public int AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public virtual Class Class { get => field ?? throw new NotLoadedException(nameof(Class)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
