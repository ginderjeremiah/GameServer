using Game.Api.Models.Attributes;
using SkillEntity = Game.Core.Entities.Skill;

namespace Game.Api.Models.Skills
{
    public class Skill : IModelFromSource<Skill, SkillEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public IEnumerable<AttributeMultiplier> DamageMultipliers { get; set; }
        public string Description { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public static Skill FromSource(SkillEntity skill)
        {
            return new Skill
            {
                Id = skill.Id,
                Name = skill.Name,
                BaseDamage = skill.BaseDamage,
                DamageMultipliers = skill.SkillDamageMultipliers.To().Model<AttributeMultiplier>(),
                Description = skill.Description,
                CooldownMS = skill.CooldownMs,
                IconPath = skill.IconPath,
            };
        }
    }
}
