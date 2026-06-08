namespace Game.Infrastructure.Entities
{
    public class PlayerStatistic
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public int StatisticTypeId { get; set; }
        public int? EntityId { get; set; }
        public decimal Value { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual StatisticType StatisticType { get => field ?? throw new NotLoadedException(nameof(StatisticType)); set; }
    }
}
