namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a tag.</summary>
    public class Tag : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int TagCategoryId { get; set; }
    }
}
