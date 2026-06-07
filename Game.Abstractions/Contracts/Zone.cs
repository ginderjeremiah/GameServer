namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a zone in the reference-data catalogue.</summary>
    public class Zone : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }
    }
}
