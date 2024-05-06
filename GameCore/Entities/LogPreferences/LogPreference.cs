using System.Data;

namespace GameCore.Entities.LogPreferences
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
