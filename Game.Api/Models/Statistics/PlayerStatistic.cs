using PlayerStatisticEntity = Game.Abstractions.Entities.PlayerStatistic;

namespace Game.Api.Models.Statistics
{
    public class PlayerStatistic : IModelFromSource<PlayerStatistic, PlayerStatisticEntity>
    {
        public int StatisticTypeId { get; set; }
        public int EntityId { get; set; }
        public long Value { get; set; }

        public static PlayerStatistic FromSource(PlayerStatisticEntity entity)
        {
            return new PlayerStatistic
            {
                StatisticTypeId = entity.StatisticTypeId,
                EntityId = entity.EntityId,
                Value = entity.Value,
            };
        }
    }
}
