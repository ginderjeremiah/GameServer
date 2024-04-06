using GameServer.Models.Items;

namespace GameServer.Models.Zones
{
    public class Zone : IModel
    {
        public int ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string ZoneDesc { get; set; }
        public int ZoneOrder { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public List<ItemDrop> ZoneDrops { get; set; }

        public Zone(DataAccess.Models.Zones.Zone zone)
        {
            ZoneId = zone.ZoneId;
            ZoneName = zone.ZoneName;
            ZoneDesc = zone.ZoneDesc;
            ZoneOrder = zone.ZoneOrder;
            LevelMin = zone.LevelMin;
            LevelMax = zone.LevelMax;
            ZoneDrops = zone.ZoneDrops.Select(drop => new ItemDrop(drop)).ToList();
        }
    }
}
