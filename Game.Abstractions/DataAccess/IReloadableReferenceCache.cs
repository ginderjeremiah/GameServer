namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// A reference-data cache that can rebuild its in-memory snapshot from the database and publish it with
    /// a single atomic swap. Implementations build the complete new snapshot off to the side while readers
    /// keep serving the previous one, then swap it in with one reference assignment, so readers stay
    /// lock-free, never observe an empty or torn snapshot, and a failed reload leaves the old snapshot in
    /// place. Discovered as a set from DI (<see cref="System.Collections.Generic.IEnumerable{T}"/> of this
    /// type) by the admin cache-reload filter and the startup initializer.
    /// </summary>
    public interface IReloadableReferenceCache
    {
        /// <summary>Rebuilds the cached snapshot from the database and atomically swaps it in.</summary>
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }
}
