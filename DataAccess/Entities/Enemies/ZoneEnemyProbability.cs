using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Enemies
{
    internal class ZoneEnemyProbability : IEntity, IProbabilityData
    {
        public decimal Probability { get; set; }
        public int Value { get; set; }
        public int Alias { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            Probability = reader["Probability"].AsDecimal();
            Value = reader["Value"].AsInt();
            Alias = reader["Alias"].AsInt();
        }
    }
}
