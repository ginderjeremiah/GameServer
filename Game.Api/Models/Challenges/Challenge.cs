using ChallengeEntity = Game.Abstractions.Entities.Challenge;

namespace Game.Api.Models.Challenges
{
    public class Challenge : IModelFromSource<Challenge, ChallengeEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ChallengeTypeId { get; set; }
        public int? TargetEntityId { get; set; }
        public int TargetCount { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        public static Challenge FromSource(ChallengeEntity entity)
        {
            return new Challenge
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ChallengeTypeId = entity.ChallengeTypeId,
                TargetEntityId = entity.TargetEntityId,
                TargetCount = entity.TargetCount,
                RewardItemId = entity.RewardItemId,
                RewardItemModId = entity.RewardItemModId,
            };
        }
    }
}
