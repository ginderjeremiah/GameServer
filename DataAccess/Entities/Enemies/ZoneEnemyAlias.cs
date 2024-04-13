using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Enemies
{
    internal class ZoneEnemyAlias : IEntity, IAliasData
    {
        public int Alias { get; set; }
        public int Value { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            Alias = reader["Alias"].AsInt();
            Value = reader["Value"].AsInt();
        }
    }
}
