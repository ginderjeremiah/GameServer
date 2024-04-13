using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.PlayerAttributes
{
    public class PlayerAttribute : IEntity
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            PlayerId = reader["PlayerId"].AsInt();
            AttributeId = reader["AttributeId"].AsInt();
            Amount = reader["Amount"].AsDecimal();
        }
    }
}
