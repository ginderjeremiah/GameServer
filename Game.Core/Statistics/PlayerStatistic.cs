namespace Game.Core.Statistics
{
    /// <summary>
    /// Represents a tracked statistic for a player.
    /// </summary>
    public class PlayerStatistic
    {
        public required EStatisticType Type { get; set; }
        public required int EntityId { get; set; }
        public required long Value { get; set; }
    }
}
