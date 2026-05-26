namespace Game.Abstractions.Entities
{
    public partial class SkillDamageMultiplier
    {
        public int SkillId { get; set; }
        public int AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public virtual Skill Skill { get => field ?? throw new NavigationNotLoadedException(nameof(Skill)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NavigationNotLoadedException(nameof(Attribute)); set; }
    }
}
