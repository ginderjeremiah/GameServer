using Game.Core;
using LogPreferenceEntity = Game.Core.Entities.LogPreference;

namespace Game.Api.Models.Player
{
    public class LogPreference : IModelFromSource<LogPreference, LogPreferenceEntity>
    {
        public ELogType Id { get; set; }
        public bool Enabled { get; set; }

        public static LogPreference FromSource(LogPreferenceEntity preference)
        {
            return new LogPreference
            {
                Id = (ELogType)preference.LogSettingId,
                Enabled = preference.Enabled,
            };
        }
    }
}
