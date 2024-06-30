namespace GameCore.Entities
{
    public class SessionData
    {
        public int CurrentZone { get; set; }
        public DateTime EarliestDefeat { get; set; }
        public bool Victory { get; set; }
        public string Id { get; set; }
        public DateTime LastUsed { get; set; }
        public DateTime EnemyCooldown { get; set; }
        public Player PlayerData { get; set; } = new();

        public SessionData(string id)
        {
            Id = id;
        }
    }
}
