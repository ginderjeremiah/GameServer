namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The kind of mutation a <see cref="Change{T}"/> represents in an admin change set. The numeric
    /// ordering is significant: change sets are applied by descending value (Delete, then Edit, then
    /// Add) so removals are flushed before insertions.
    /// </summary>
    public enum EChangeType
    {
        Add = 0,
        Edit = 1,
        Delete = 2
    }
}
