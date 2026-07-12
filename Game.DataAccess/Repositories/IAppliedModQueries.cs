namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Queries over players' applied mods for the Content Authoring admin persistence, which needs to know
    /// whether an item mod slot is currently occupied before allowing its deletion. Applied mods are
    /// player-owned write-behind data, never cached in the admin tier, so — like <see cref="ITagAssignmentQueries"/>
    /// — this queries the database directly.
    /// </summary>
    internal interface IAppliedModQueries
    {
        /// <summary>The subset of the given item mod slot ids that are currently occupied by at least one
        /// player's applied mod.</summary>
        IAsyncEnumerable<int> GetOccupiedSlotIds(IReadOnlyCollection<int> slotIds);
    }
}
