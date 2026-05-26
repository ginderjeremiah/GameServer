namespace Game.Abstractions.Entities
{
    public partial class EnemySkill
    {
        public int EnemyId { get; set; }
        public int SkillId { get; set; }

        public virtual Enemy Enemy { get => field ?? throw new NavigationNotLoadedException(nameof(Enemy)); set; }
        public virtual Skill Skill { get => field ?? throw new NavigationNotLoadedException(nameof(Skill)); set; }
    }
}
