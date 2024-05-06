using System.Data;

namespace GameCore.Entities.Enemies
{
    public class ZoneEnemyAlias : IEntity, IAliasData
    {
        public int Alias { get; set; }
        public int Value { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            Alias = record["Alias"].AsInt();
            Value = record["Value"].AsInt();
        }
    }
}
