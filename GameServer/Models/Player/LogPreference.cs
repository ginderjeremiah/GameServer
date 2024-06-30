namespace GameServer.Models.Player
{
    public class LogPreference : IModel
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public LogPreference() { }

        public LogPreference(GameCore.Entities.LogPreference preference)
        {
            Name = preference.LogSetting.Name;
            Enabled = preference.Enabled;
        }
    }
}
