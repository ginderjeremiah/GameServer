namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a tag category.</summary>
    public class TagCategory : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
