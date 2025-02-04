namespace Game.Abstractions.Entities
{
    public partial class EnemySkill
    {
        public int EnemyId { get; set; }
        public int SkillId { get; set; }

        public virtual Enemy Enemy { get; set; }
        public virtual Skill Skill { get; set; }
    }
}
