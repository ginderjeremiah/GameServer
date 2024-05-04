using GameCore;
using System.Data;

namespace DataAccess.Entities.Drops
{
    public class ZoneDrop : IDrop
    {
        public int ZoneDropId { get; set; }
        public int ZoneId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ZoneDropId = record["ZoneDropId"].AsInt();
            ZoneId = record["ZoneId"].AsInt();
            ItemId = record["ItemId"].AsInt();
            DropRate = record["DropRate"].AsDecimal();
        }
    }
}
