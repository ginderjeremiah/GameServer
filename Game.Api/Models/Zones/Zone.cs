using Game.Api.Models.Items;
using ZoneEntity = Game.Core.Entities.Zone;

namespace Game.Api.Models.Zones
{
    public class Zone : IModelFromSource<Zone, ZoneEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
        public IEnumerable<ItemDrop> ZoneDrops { get; set; }

        public static Zone FromSource(ZoneEntity zone)
        {
            return new Zone
            {
                Id = zone.Id,
                Name = zone.Name,
                Description = zone.Description,
                Order = zone.Order,
                LevelMin = zone.LevelMin,
                LevelMax = zone.LevelMax,
                ZoneDrops = zone.ZoneDrops.To().Model<ItemDrop>(),
            };
        }
    }
}
