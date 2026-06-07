namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an item-mod type (the kind of mod a slot accepts).</summary>
    public class ItemModType : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
