namespace Game.Abstractions.Entities
{
    public partial class Skill : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public string Description { get; set; }
        public int CooldownMs { get; set; }
        public string IconPath { get; set; }

        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get; set; } = [];
        public virtual List<EnemySkill> EnemySkills { get; set; } = [];
        public virtual List<PlayerSkill> PlayerSkills { get; set; } = [];
    }
}
