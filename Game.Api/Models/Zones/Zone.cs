using ZoneEntity = Game.Abstractions.Entities.Zone;

namespace Game.Api.Models.Zones
{
    public class Zone : IModelFromSource<Zone, ZoneEntity>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }

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
            };
        }
    }
}
