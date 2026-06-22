namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// The attribute bonus a proficiency grants <em>on reaching</em> a given level. A player's total
    /// proficiency bonus is the sum of the increments for every level they have reached, so a linear ramp and
    /// a milestone spike are expressed uniformly as rows. Mirrors the <see cref="SkillEffect"/> attribute/
    /// modifier-type/amount storage. Keyed by (proficiency, level, attribute).
    /// </summary>
    public class ProficiencyLevelModifier
    {
        public int ProficiencyId { get; set; }
        public int Level { get; set; }
        public int AttributeId { get; set; }
        public int ModifierType { get; set; }
        public decimal Amount { get; set; }

        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
