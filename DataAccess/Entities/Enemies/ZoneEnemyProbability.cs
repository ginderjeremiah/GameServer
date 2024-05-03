using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Enemies
{
    internal class ZoneEnemyProbability : IEntity, IProbabilityData
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
