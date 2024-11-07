namespace Game.Core.Entities
{
    public partial class LogSetting
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool DefaultValue { get; set; }

        public virtual List<LogPreference> LogPreferences { get; set; }
    }
}
