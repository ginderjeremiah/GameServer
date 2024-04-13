using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Drops
{
    public class ZoneDrop : IDrop
    {
        public int ZoneDropId { get; set; }
        public int ZoneId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ZoneDropId = reader["ZoneDropId"].AsInt();
            ZoneId = reader["ZoneId"].AsInt();
            ItemId = reader["ItemId"].AsInt();
            DropRate = reader["DropRate"].AsDecimal();
        }
    }
}
