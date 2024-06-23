using System.Data;

namespace GameCore.Entities.LogPreferences
{
    public class LogPreference : IEntity
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            PlayerId = record["PlayerId"].AsInt();
            Name = record["Name"].AsString();
            Enabled = record["Enabled"].AsBool();
        }
    }
}
