namespace Game.Infrastructure.Entities
{
    public class SkillEffect
    {
        public int Id { get; set; }
        public int SkillId { get; set; }
        public int Target { get; set; }
        public int AttributeId { get; set; }
        public int ModifierType { get; set; }
        public decimal Amount { get; set; }
        public int DurationMs { get; set; }

        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
