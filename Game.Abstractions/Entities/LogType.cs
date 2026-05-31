namespace Game.Abstractions.Entities
{
    public class LogType
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool DefaultValue { get; set; }

        public virtual List<LogPreference> LogPreferences { get => field ?? throw new NotLoadedException(nameof(LogPreferences)); set; }
    }
}
