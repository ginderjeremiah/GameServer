namespace GameServer.Models.Player
{
    public class LogPreference : IModel
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public LogPreference() { }
        public LogPreference(DataAccess.Models.LogPreferences.LogPreference preference)
        {
            Name = preference.Name;
            Enabled = preference.Enabled;
        }
    }
}
