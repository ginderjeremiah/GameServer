namespace Game.Core.Statistics
{
    public class StatisticType
    {
        public EStatisticType Id { get; }
        public string Name { get; }

        public StatisticType(EStatisticType id)
        {
            Id = id;
            Name = id.ToString().SpaceWords();
        }
    }
}
