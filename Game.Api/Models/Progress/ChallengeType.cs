using Game.Core;

namespace Game.Api.Models.Progress
{
    public class ChallengeType : IModelFromSource<ChallengeType, Core.Progress.ChallengeType>
    {
        public EChallengeType Id { get; init; }
        public StatisticType? StatisticType { get; init; }
        public EChallengeGoalComparison GoalComparison { get; init; }
        public required string Name { get; init; }

        public static ChallengeType FromSource(Core.Progress.ChallengeType source)
        {
            return new ChallengeType
            {
                Id = source.Id,
                StatisticType = source.StatisticType is null ? null : StatisticType.FromSource(source.StatisticType),
                GoalComparison = source.GoalComparison,
                Name = source.Name
            };
        }
    }
}
