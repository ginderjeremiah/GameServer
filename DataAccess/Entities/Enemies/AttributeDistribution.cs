using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Enemies
{
    public class AttributeDistribution : IEntity
    {
        public int EnemyId { get; set; }
        public int AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            EnemyId = reader["EnemyId"].AsInt();
            AttributeId = reader["AttributeId"].AsInt();
            BaseAmount = reader["BaseAmount"].AsDecimal();
            AmountPerLevel = reader["AmountPerLevel"].AsDecimal();
        }
    }
}
