using Game.Core;
using CoreChallenge = Game.Core.Challenges.Challenge;
using CorePlayerChallenge = Game.Core.Challenges.PlayerChallenge;
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
                Type = (EChallengeType)entity.ChallengeTypeId,
                StatisticType = (EStatisticType?)entity.StatisticTypeId,
                EntityType = (EEntityType)entity.EntityTypeId,
                TargetEntityId = entity.TargetEntityId,
                ProgressGoal = entity.ProgressGoal,
                RewardItemId = entity.RewardItemId,
                RewardItemModId = entity.RewardItemModId,
            };
        }

        public static CorePlayerChallenge ToCore(EntityPlayerChallenge entity, int progressGoal)
        {
            return new CorePlayerChallenge
            {
                ChallengeId = entity.ChallengeId,
                Progress = entity.Progress,
                ProgressGoal = progressGoal,
                Completed = entity.Completed,
                CompletedAt = entity.CompletedAt,
            };
        }
    }
}
