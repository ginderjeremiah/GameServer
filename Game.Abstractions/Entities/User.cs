namespace Game.Abstractions.Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public Guid Salt { get; set; }
        public required string PassHash { get; set; }
        public DateTime LastLogin { get; set; }

        public virtual List<Player> Players { get => field ?? throw new NotLoadedException(nameof(Players)); set; }
    }
}
