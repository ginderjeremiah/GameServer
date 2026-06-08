namespace Game.Infrastructure.Entities
{
    public class ChallengeType
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int? StatisticTypeId { get; set; }

        public virtual StatisticType? StatisticType { get; set; }
    }
}
