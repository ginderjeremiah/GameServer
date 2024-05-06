using System.Data;

namespace GameCore.Entities.Enemies
{
    public class ZoneEnemyProbability : IEntity, IProbabilityData
    {
        public decimal Probability { get; set; }
        public int Value { get; set; }
        public int Alias { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            Probability = record["Probability"].AsDecimal();
            Value = record["Value"].AsInt();
            Alias = record["Alias"].AsInt();
        }
    }
}
