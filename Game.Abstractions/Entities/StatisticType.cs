namespace Game.Abstractions.Entities
{
    public class StatisticType
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<PlayerStatistic> PlayerStatistics { get => field ?? throw new NavigationNotLoadedException(nameof(PlayerStatistics)); set; }
    }
}
