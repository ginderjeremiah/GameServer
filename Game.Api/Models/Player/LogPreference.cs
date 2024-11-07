using Game.Core;
using LogPreferenceEntity = Game.Core.Entities.LogPreference;

namespace Game.Api.Models.Player
{
    public class LogPreference : IModelFromSource<LogPreference, LogPreferenceEntity>
    {
        public ELogSetting Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public static LogPreference FromSource(LogPreferenceEntity preference)
        {
            return new LogPreference
            {
                Id = (ELogSetting)preference.LogSettingId,
                Name = preference.LogSetting.Name,
                Enabled = preference.Enabled,
            };
        }
    }
}
