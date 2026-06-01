using Game.Core;
using CoreChallenge = Game.Core.Challenges.Challenge;

namespace Game.Api.Models.Challenges
{
    public class Challenge : IModelFromSource<Challenge, CoreChallenge>
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EChallengeType ChallengeTypeId { get; set; }
        public EStatisticType? StatisticType { get; set; }
        public EEntityType EntityType { get; set; }
        public int? TargetEntityId { get; set; }
        public decimal ProgressGoal { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        public static Challenge FromSource(CoreChallenge challenge)
        {
            return new Challenge
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                ChallengeTypeId = challenge.Type,
                StatisticType = challenge.StatisticType,
                EntityType = challenge.EntityType,
                TargetEntityId = challenge.TargetEntityId,
                ProgressGoal = challenge.ProgressGoal,
                RewardItemId = challenge.RewardItemId,
                RewardItemModId = challenge.RewardItemModId,
            };
        }
    }
}
