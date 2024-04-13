using GameLibrary;
using System.Data.SqlClient;

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

        public void LoadFromReader(SqlDataReader reader)
        {
            SkillId = reader["SkillId"].AsInt();
            SkillName = reader["SkillName"].AsString();
            BaseDamage = reader["BaseDamage"].AsDecimal();
            SkillDesc = reader["SkillDesc"].AsString();
            CooldownMS = reader["CooldownMS"].AsInt();
            IconPath = reader["IconPath"].AsString();
        }
    }
}
