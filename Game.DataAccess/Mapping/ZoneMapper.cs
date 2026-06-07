using Contracts = Game.Abstractions.Contracts;
using EntityZone = Game.Abstractions.Entities.Zone;

namespace Game.DataAccess.Mapping
{
    internal static class ZoneMapper
    {
        /// <summary>Maps an entity <see cref="EntityZone"/> to the reference-data read
        /// <see cref="Contracts.Zone"/> contract.</summary>
        public static Contracts.Zone ToContract(EntityZone entity)
        {
            return new Contracts.Zone
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Order = entity.Order,
                LevelMin = entity.LevelMin,
                LevelMax = entity.LevelMax,
            };
        }
    }
}
