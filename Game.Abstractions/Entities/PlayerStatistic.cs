namespace Game.Abstractions.Entities
{
    public class PlayerStatistic
    {
        public int PlayerId { get; set; }
        public int StatisticTypeId { get; set; }
        public int EntityId { get; set; }
        public long Value { get; set; }

        public virtual Player Player { get; set; }
    }
}
