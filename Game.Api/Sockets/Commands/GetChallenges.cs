using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Core;
using CoreChallenge = Game.Core.Progress.Challenge;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full challenge reference-data collection. WebSocket equivalent
    /// of the <c>GET /api/Challenges</c> endpoint.
    /// </summary>
    public class GetChallenges : AbstractReferenceDataCommand<Challenge>
    {
        private readonly IChallenges _challenges;

        public override string Name { get; set; } = nameof(GetChallenges);

        public GetChallenges(IChallenges challenges)
        {
            _challenges = challenges;
        }

        protected override IEnumerable<Challenge> GetReferenceData()
        {
            return _challenges.All().Select(ToContract);
        }

        // The domain challenge carries the rich ChallengeType; the read contract flattens it to the
        // type id plus the statistic/entity dimensions the type derives. Kept here because the
        // gameplay domain (IChallenges.All) intentionally serves the domain model — see backend.md.
        private static Challenge ToContract(CoreChallenge challenge)
        {
            return new Challenge
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                ChallengeTypeId = challenge.Type.Id,
                StatisticType = challenge.Type.StatisticType?.Id,
                EntityType = challenge.Type.StatisticType?.EntityType ?? EEntityType.None,
                TargetEntityId = challenge.TargetEntityId,
                ProgressGoal = challenge.ProgressGoal,
                RewardItemId = challenge.RewardItemId,
                RewardItemModId = challenge.RewardItemModId,
                RewardSkillId = challenge.RewardSkillId,
                RetiredAt = challenge.RetiredAt,
            };
        }
    }
}
