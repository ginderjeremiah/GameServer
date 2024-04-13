using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.LogPreferences
{
    public class LogPreference : IEntity
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            Name = reader["Name"].AsString();
            Enabled = reader["Enabled"].AsBool();
        }
    }
}
