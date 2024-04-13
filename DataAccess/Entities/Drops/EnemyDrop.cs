using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Drops
{
    public class EnemyDrop : IDrop
    {
        public int EnemyDropId { get; set; }
        public int EnemyId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            EnemyDropId = reader["EnemyDropId"].AsInt();
            EnemyId = reader["EnemyId"].AsInt();
            ItemId = reader["ItemId"].AsInt();
            DropRate = reader["DropRate"].AsDecimal();
        }
    }
}
