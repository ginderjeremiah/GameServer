using DataAccess.Models.Attributes;
using GameLibrary;
using System.Data;

namespace DataAccess.Models.Skills
{
    public class Skill
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public decimal BaseDamage { get; set; }
        public List<AttributeMultiplier> DamageMultipliers { get; set; }
        public string SkillDesc { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public Skill(DataRow dataRow, List<AttributeMultiplier> multipliers)
        {
            SkillId = dataRow["SkillId"].AsInt();
            SkillName = dataRow["SkillName"].AsString();
            BaseDamage = dataRow["BaseDamage"].AsDecimal();
            DamageMultipliers = multipliers;
            SkillDesc = dataRow["SkillDesc"].AsString();
            CooldownMS = dataRow["CooldownMS"].AsInt();
            IconPath = dataRow["IconPath"].AsString();
        }
    }
}
