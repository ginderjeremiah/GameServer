using Game.Core;
using Game.Core.Progress;
using CorePlayerChallenge = Game.Core.Progress.PlayerChallenge;
using CorePlayerStatistic = Game.Core.Progress.PlayerStatistic;
using EntityPlayerChallenge = Game.Abstractions.Entities.PlayerChallenge;
using EntityPlayerStatistic = Game.Abstractions.Entities.PlayerStatistic;

namespace Game.DataAccess.Mapping
{
    internal static class PlayerProgressMapper
    {
        public static CorePlayerStatistic ToCore(EntityPlayerStatistic entity)
        {
            return new CorePlayerStatistic
            {
                Type = (EStatisticType)entity.StatisticTypeId,
                EntityId = entity.EntityId,
                Value = entity.Value,
            };
        }

        public static CorePlayerChallenge ToCore(EntityPlayerChallenge entity, Challenge challenge)
        {
            return new CorePlayerChallenge(challenge, entity.Progress, entity.Completed, entity.CompletedAt);
        }
    }
}
