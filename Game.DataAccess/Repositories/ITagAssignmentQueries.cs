namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal queries over the tag-assignment join tables for the Content Authoring admin persistence,
    /// which reconciles a single record's assignments directly. Returns plain tag ids (never tag entities or
    /// their full item / item-mod membership), so the admin <c>SetTags</c> path can diff and add/remove one
    /// join row at a time. Distinct from the public <see cref="Abstractions.DataAccess.ITags"/> read
    /// contracts. Unlike the cached reference repos these query the database directly.
    /// </summary>
    internal interface ITagAssignmentQueries
    {
        /// <summary>The ids of the tags currently assigned to the given item.</summary>
        IAsyncEnumerable<int> GetTagIdsForItem(int itemId);

        /// <summary>The ids of the tags currently assigned to the given item mod.</summary>
        IAsyncEnumerable<int> GetTagIdsForItemMod(int itemModId);

        /// <summary>The subset of the given ids that correspond to existing tags, so a join row is never
        /// inserted for an unknown tag (which would violate the foreign key).</summary>
        IAsyncEnumerable<int> GetExistingTagIds(IEnumerable<int> tagIds);
    }
}
