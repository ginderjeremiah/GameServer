using GameServer.Models.Attributes;
namespace GameServer.Models.Skills
{
    public class Skill : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public List<AttributeMultiplier> DamageMultipliers { get; set; }
        public string Description { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public Skill(GameCore.Entities.Skill skill)
        {
            Id = skill.Id;
            Name = skill.Name;
            BaseDamage = skill.BaseDamage;
            DamageMultipliers = skill.SkillDamageMultipliers.Select(x => new AttributeMultiplier(x)).ToList();
            Description = skill.Description;
            CooldownMS = skill.CooldownMS;
            IconPath = skill.IconPath;
        }
    }
}
