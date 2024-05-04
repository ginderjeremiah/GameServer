using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.LogPreferences
{
    public class LogPreference : IEntity
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            Name = record["Name"].AsString();
            Enabled = record["Enabled"].AsBool();
        }
    }
}
