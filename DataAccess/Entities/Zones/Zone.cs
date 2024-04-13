using DataAccess.Entities.Drops;
using GameLibrary;
using System.Data.SqlClient;

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

        public void LoadFromReader(SqlDataReader reader)
        {
            ZoneId = reader["ZoneId"].AsInt();
            ZoneName = reader["ZoneName"].AsString();
            ZoneDesc = reader["ZoneDesc"].AsString();
            ZoneOrder = reader["ZoneOrder"].AsInt();
            LevelMin = reader["LevelMin"].AsInt();
            LevelMax = reader["LevelMax"].AsInt();
        }
    }
}
