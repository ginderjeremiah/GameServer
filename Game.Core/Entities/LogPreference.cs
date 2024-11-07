using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class LogPreference
    {
        public int PlayerId { get; set; }
        public int LogSettingId { get; set; }
        public bool Enabled { get; set; }

        [JsonIgnore]
        public virtual Player Player { get; set; }
        public virtual LogSetting LogSetting { get; set; }
    }
}
