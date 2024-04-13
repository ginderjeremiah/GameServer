namespace GameServer.Models.Player
{
    public class LogPreference : IModel
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public LogPreference() { }
        public LogPreference(DataAccess.Entities.LogPreferences.LogPreference preference)
        {
            Name = preference.Name;
            Enabled = preference.Enabled;
        }
    }
}
