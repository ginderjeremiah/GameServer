namespace Game.Infrastructure.Entities
{
    public class SkillDamageMultiplier
    {
        public int SkillId { get; set; }
        public int AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
