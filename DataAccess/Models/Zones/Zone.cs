using DataAccess.Models.Items;

namespace DataAccess.Models.Zones
{
    public class Zone
    {
        public int ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string ZoneDesc { get; set; }
        public int ZoneOrder { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public List<ItemDrop> ZoneDrops { get; set; }
    }
}
