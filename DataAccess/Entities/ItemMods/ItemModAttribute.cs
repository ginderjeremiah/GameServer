using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.ItemMods
{
    public class ItemModAttribute : IEntity
    {
        public int ItemModId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemModId = reader["ItemModId"].AsInt();
            AttributeId = reader["AttributeId"].AsInt();
            Amount = reader["Amount"].AsDecimal();
        }
    }
}
