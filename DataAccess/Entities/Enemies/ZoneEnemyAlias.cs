using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Enemies
{
    internal class ZoneEnemyAlias : IEntity, IAliasData
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
