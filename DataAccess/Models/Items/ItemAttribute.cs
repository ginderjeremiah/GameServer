using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.Items
{
    public class ItemAttribute : IDataModel
    {
        public int ItemId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemId = reader["ItemId"].AsInt();
            AttributeId = reader["AttributeId"].AsInt();
            Amount = reader["Amount"].AsDecimal();
        }
    }
}
