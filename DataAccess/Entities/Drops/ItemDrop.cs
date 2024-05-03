using GameLibrary;
using System.Data;

namespace DataAccess.Entities.Drops
{
    public class ItemDrop : IDrop
    {
        public int DroppedById { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            DroppedById = record["DroppedById"].AsInt();
            ItemId = record["ItemId"].AsInt();
            DropRate = record["DropRate"].AsDecimal();
        }
    }
}
