namespace Game.Infrastructure.Entities
{
    /// <summary>A skill a <see cref="Class"/> grants (selected) at character creation — a pure join row.</summary>
    public class ClassStarterSkill
    {
        public int ClassId { get; set; }
        public int SkillId { get; set; }

        public virtual Class Class { get => field ?? throw new NotLoadedException(nameof(Class)); set; }
        public virtual Skill Skill { get => field ?? throw new NotLoadedException(nameof(Skill)); set; }
    }
}
