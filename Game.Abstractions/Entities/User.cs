namespace Game.Abstractions.Entities
{
    public class User
    {
        public string Username { get; set; }
        public Guid Salt { get; set; }
        public string PassHash { get; set; }
        public DateTime LastLogin { get; set; }

        public virtual List<Player> Players { get; set; }
    }
}
