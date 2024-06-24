namespace GameCore.Entities
{
    public class LogPreference
    {
        public int PlayerId { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }

        public virtual Player Player { get; set; }
    }
}
