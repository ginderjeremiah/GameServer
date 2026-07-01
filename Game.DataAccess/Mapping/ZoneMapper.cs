using Contracts = Game.Abstractions.Contracts;
using CoreZone = Game.Core.Zones.Zone;
using EntityZone = Game.Infrastructure.Entities.Zone;

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
                BossEnemyId = entity.BossEnemyId,
                BossLevel = entity.BossLevel,
                UnlockChallengeId = entity.UnlockChallengeId,
                IsHome = entity.IsHome,
                DesignerNotes = entity.DesignerNotes,
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>Maps an entity <see cref="EntityZone"/> to the lean gameplay <see cref="CoreZone"/>
        /// domain model used for battle setup. Display-only fields (name, description, order) stay on the
        /// read contract; the domain model carries only what the encounter rules need.</summary>
        public static CoreZone ToCore(EntityZone entity)
        {
            return new CoreZone
            {
                Id = entity.Id,
                LevelMin = entity.LevelMin,
                LevelMax = entity.LevelMax,
                BossEnemyId = entity.BossEnemyId,
                BossLevel = entity.BossLevel,
                UnlockChallengeId = entity.UnlockChallengeId,
            };
        }
    }
}
