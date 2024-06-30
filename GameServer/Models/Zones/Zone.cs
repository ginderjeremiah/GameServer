using GameServer.Models.Items;

namespace GameServer.Models.Zones
{
    public class Zone : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public List<ItemDrop> ZoneDrops { get; set; }

        public Zone(GameCore.Entities.Zone zone)
        {
            Id = zone.Id;
            Name = zone.Name;
            Description = zone.Description;
            Order = zone.Order;
            LevelMin = zone.LevelMin;
            LevelMax = zone.LevelMax;
            ZoneDrops = zone.ZoneDrops.Select(drop => new ItemDrop(drop)).ToList();
        }
    }
}
