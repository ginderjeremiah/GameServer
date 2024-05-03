using DataAccess.Entities.Drops;
using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Zones
{
    public class Zone : IEntity
    {
        public int ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string ZoneDesc { get; set; }
        public int ZoneOrder { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public List<ItemDrop> ZoneDrops { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ZoneId = record["ZoneId"].AsInt();
            ZoneName = record["ZoneName"].AsString();
            ZoneDesc = record["ZoneDesc"].AsString();
            ZoneOrder = record["ZoneOrder"].AsInt();
            LevelMin = record["LevelMin"].AsInt();
            LevelMax = record["LevelMax"].AsInt();
        }
    }
}
