using Game.Core;
using CoreChallenge = Game.Core.Progress.Challenge;
using CoreChallengeType = Game.Core.Progress.ChallengeType;
using CorePlayerChallenge = Game.Core.Progress.PlayerChallenge;
using EntityChallenge = Game.Abstractions.Entities.Challenge;
using EntityPlayerChallenge = Game.Abstractions.Entities.PlayerChallenge;

namespace Game.DataAccess.Mapping
{
    internal static class ChallengeMapper
    {
        public static CoreChallenge ToCore(EntityChallenge entity)
        {
            return new CoreChallenge
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description ?? string.Empty,
                Type = new CoreChallengeType((EChallengeType)entity.ChallengeTypeId),
                TargetEntityId = entity.TargetEntityId,
                ProgressGoal = entity.ProgressGoal,
                RewardItemId = entity.RewardItemId,
                RewardItemModId = entity.RewardItemModId,
            };
        }

        public static CorePlayerChallenge ToCore(EntityPlayerChallenge entity, EntityChallenge entityChallenge)
        {
            return new CorePlayerChallenge(ToCore(entityChallenge), entity.Progress, entity.Completed, entity.CompletedAt);
        }
    }
}
