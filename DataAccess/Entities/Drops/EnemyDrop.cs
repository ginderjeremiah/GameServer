using GameCore;
using System.Data;

namespace DataAccess.Entities.Drops
{
    public class EnemyDrop : IDrop
    {
        public int EnemyDropId { get; set; }
        public int EnemyId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            EnemyDropId = record["EnemyDropId"].AsInt();
            EnemyId = record["EnemyId"].AsInt();
            ItemId = record["ItemId"].AsInt();
            DropRate = record["DropRate"].AsDecimal();
        }
    }
}
