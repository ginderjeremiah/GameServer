using CoreChallenge = Game.Core.Challenges.Challenge;

namespace Game.Api.Models.Challenges
{
    public class Challenge : IModelFromSource<Challenge, CoreChallenge>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ChallengeTypeId { get; set; }
        public int? TargetEntityId { get; set; }
        public int TargetCount { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        public static Challenge FromSource(CoreChallenge challenge)
        {
            return new Challenge
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                ChallengeTypeId = (int)challenge.Type,
                TargetEntityId = challenge.TargetEntityId,
                TargetCount = challenge.TargetCount,
                RewardItemId = challenge.RewardItemId,
                RewardItemModId = challenge.RewardItemModId,
            };
        }
    }
}
