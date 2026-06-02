using Game.Core;
using PlayerStatisticEntity = Game.Abstractions.Entities.PlayerStatistic;

namespace Game.Api.Models.Progress
{
    public class PlayerStatistic : IModelFromSource<PlayerStatistic, PlayerStatisticEntity>
    {
        public EStatisticType StatisticTypeId { get; set; }
        public int? EntityId { get; set; }
        public decimal Value { get; set; }

        public static PlayerStatistic FromSource(PlayerStatisticEntity entity)
        {
            return new PlayerStatistic
            {
                StatisticTypeId = (EStatisticType)entity.StatisticTypeId,
                EntityId = entity.EntityId,
                Value = entity.Value,
            };
        }
    }
}
