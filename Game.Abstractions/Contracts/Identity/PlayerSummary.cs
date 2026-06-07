namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>A lightweight admin view of one of a user's players.</summary>
    public class PlayerSummary : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int Level { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
