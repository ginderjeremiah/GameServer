using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class Skill
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public string Description { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        [JsonIgnore]
        public virtual List<EnemySkill> EnemySkills { get; set; }
        [JsonIgnore]
        public virtual List<PlayerSkill> PlayerSkills { get; set; }
    }
}
