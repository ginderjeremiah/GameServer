using GameServer.Models.Attributes;
namespace GameServer.Models.Skills
{
    public class Skill : IModel
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public decimal BaseDamage { get; set; }
        public List<AttributeMultiplier> DamageMultipliers { get; set; }
        public string SkillDesc { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public Skill(GameCore.Entities.Skills.Skill skill)
        {
            SkillId = skill.SkillId;
            SkillName = skill.SkillName;
            BaseDamage = skill.BaseDamage;
            DamageMultipliers = skill.DamageMultipliers.Select(x => new AttributeMultiplier(x)).ToList();
            SkillDesc = skill.SkillDesc;
            CooldownMS = skill.CooldownMS;
            IconPath = skill.IconPath;
        }
    }
}
