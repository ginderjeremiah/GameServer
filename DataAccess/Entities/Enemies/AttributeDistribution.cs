using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Enemies
{
    public class AttributeDistribution : IEntity
    {
        public int EnemyId { get; set; }
        public int AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            EnemyId = record["EnemyId"].AsInt();
            AttributeId = record["AttributeId"].AsInt();
            BaseAmount = record["BaseAmount"].AsDecimal();
            AmountPerLevel = record["AmountPerLevel"].AsDecimal();
        }
    }
}
