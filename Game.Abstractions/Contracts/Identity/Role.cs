namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>Read contract for an access role that can be granted to a user.</summary>
    public class Role : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
