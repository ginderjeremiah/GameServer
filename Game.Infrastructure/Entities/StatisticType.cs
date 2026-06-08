namespace Game.Infrastructure.Entities
{
    public class StatisticType
    {
        public int Id { get; set; }
        public int EntityType { get; set; }
        public required string Name { get; set; }

        public virtual List<PlayerStatistic> PlayerStatistics { get => field ?? throw new NotLoadedException(nameof(PlayerStatistics)); set; }
    }
}
