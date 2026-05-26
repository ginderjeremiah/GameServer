namespace Game.Abstractions.Entities
{
    public class PlayerStatistic
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public int StatisticTypeId { get; set; }
        public int? EntityId { get; set; }
        public long Value { get; set; }

        public virtual Player Player { get => field ?? throw new NavigationNotLoadedException(nameof(Player)); set; }
    }
}
