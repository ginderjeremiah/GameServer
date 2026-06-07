using Game.Core;
using CorePlayerStatistic = Game.Core.Progress.PlayerStatistic;

namespace Game.Api.Models.Progress
{
    public class PlayerStatistic : IModelFromSource<PlayerStatistic, CorePlayerStatistic>
    {
        public EStatisticType StatisticTypeId { get; set; }
        public int? EntityId { get; set; }
        public decimal Value { get; set; }

        public static PlayerStatistic FromSource(CorePlayerStatistic source)
        {
            return new PlayerStatistic
            {
                StatisticTypeId = source.Type,
                EntityId = source.EntityId,
                Value = source.Value,
            };
        }
    }
}
