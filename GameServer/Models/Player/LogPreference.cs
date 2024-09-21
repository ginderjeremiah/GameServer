using GameCore;

namespace GameServer.Models.Player
{
    public class LogPreference : IModel
    {
        public ELogSetting Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public LogPreference() { }

        public LogPreference(GameCore.Entities.LogPreference preference)
        {
            Id = (ELogSetting)preference.LogSettingId;
            Name = preference.LogSetting.Name;
            Enabled = preference.Enabled;
        }
    }
}
