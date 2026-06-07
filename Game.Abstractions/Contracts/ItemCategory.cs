namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an item category.</summary>
    public class ItemCategory : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
