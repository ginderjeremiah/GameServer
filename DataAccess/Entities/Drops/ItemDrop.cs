using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Drops
{
    public class ItemDrop : IDrop
    {
        public int DroppedById { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            DroppedById = reader["DroppedById"].AsInt();
            ItemId = reader["ItemId"].AsInt();
            DropRate = reader["DropRate"].AsDecimal();
        }
    }
}
