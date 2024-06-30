namespace GameCore.Entities
{
    public class LogSetting
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool DefaultValue { get; set; }

        public virtual List<LogPreference> LogPreferences { get; set; }
    }
}
