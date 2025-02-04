namespace Game.Abstractions.Entities
{
    public partial class LogPreference
    {
        public int PlayerId { get; set; }
        public int LogSettingId { get; set; }
        public bool Enabled { get; set; }

        public virtual Player Player { get; set; }
        public virtual LogSetting LogSetting { get; set; }
    }
}
