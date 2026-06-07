namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of tag ids to associate with a single item or item mod (<see cref="Id"/>).</summary>
    public class SetTagsData
    {
        public int Id { get; set; }
        public required List<int> TagIds { get; set; }
    }
}
