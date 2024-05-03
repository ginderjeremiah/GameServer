using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Skills
{
    public class Skill : IEntity
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public decimal BaseDamage { get; set; }
        public List<SkillDamageMultiplier> DamageMultipliers { get; set; }
        public string SkillDesc { get; set; }
        public int CooldownMS { get; set; }
        public string IconPath { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            SkillId = record["SkillId"].AsInt();
            SkillName = record["SkillName"].AsString();
            BaseDamage = record["BaseDamage"].AsDecimal();
            SkillDesc = record["SkillDesc"].AsString();
            CooldownMS = record["CooldownMS"].AsInt();
            IconPath = record["IconPath"].AsString();
        }
    }
}
