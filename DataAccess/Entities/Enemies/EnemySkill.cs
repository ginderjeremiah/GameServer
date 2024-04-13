using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Enemies
{
    internal class EnemySkill : IEntity
    {
        public int EnemyId { get; set; }
        public int SkillId { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            EnemyId = reader["EnemyId"].AsInt();
            SkillId = reader["SkillId"].AsInt();
        }
    }
}
