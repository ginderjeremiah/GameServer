namespace Game.Abstractions.Entities
{
    public partial class LogPreference
    {
        public int PlayerId { get; set; }
        public int LogSettingId { get; set; }
        public bool Enabled { get; set; }

        public virtual Player Player { get => field ?? throw new NavigationNotLoadedException(nameof(Player)); set; }
        public virtual LogSetting LogSetting { get => field ?? throw new NavigationNotLoadedException(nameof(LogSetting)); set; }
    }
}
