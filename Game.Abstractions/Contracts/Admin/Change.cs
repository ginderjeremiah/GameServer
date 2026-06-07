namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A single create/update/delete operation against a content-authoring record, paired with the
    /// record it applies to. The admin write language ("Content Authoring" context) is expressed as
    /// batches of these.
    /// </summary>
    public class Change<T> : IModel where T : IModel
    {
        public required T Item { get; set; }
        public EChangeType ChangeType { get; set; }
    }
}
