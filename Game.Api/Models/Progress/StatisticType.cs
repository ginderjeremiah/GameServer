using Game.Core;

namespace Game.Api.Models.Progress
{
    public class StatisticType : IModelFromSource<StatisticType, Core.Progress.StatisticType>
    {
        public EStatisticType Id { get; init; }
        public EEntityType EntityType { get; init; }
        public bool BossOnly { get; init; }
        public required string Name { get; init; }

        public static StatisticType FromSource(Core.Progress.StatisticType source)
        {
            return new StatisticType
            {
                Id = source.Id,
                EntityType = source.EntityType,
                BossOnly = source.BossOnly,
                Name = source.Name
            };
        }
    }
}
