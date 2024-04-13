using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Skills
{
    public class SkillDamageMultiplier : IEntity
    {
        public int SkillId { get; set; }
        public int AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            SkillId = reader["SkillId"].AsInt();
            AttributeId = reader["AttributeId"].AsInt();
            Multiplier = reader["Multiplier"].AsDecimal();
        }
    }
}
