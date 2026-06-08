namespace Game.Infrastructure.Entities
{
    public class LogPreference
    {
        public int PlayerId { get; set; }
        public int LogTypeId { get; set; }
        public bool Enabled { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual LogType LogType { get => field ?? throw new NotLoadedException(nameof(LogType)); set; }
    }
}
