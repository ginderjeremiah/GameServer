using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// Base class for the singleton reference-data cache holders. Each holder owns the immutable snapshot
    /// for one reference set and rebuilds it from the database on <see cref="ReloadAsync"/>: it builds the
    /// complete new snapshot off to the side and publishes it with a single atomic reference assignment, so
    /// readers access <see cref="Current"/> lock-free, keep serving the previous snapshot until the swap,
    /// and a failed reload leaves the previous snapshot in place. The holder creates its own DI scope (and
    /// therefore its own <see cref="GameContext"/>) for the reload query, so the load does not borrow
    /// whichever request-scoped context happens to be in flight.
    /// </summary>
    /// <typeparam name="TSnapshot">The immutable snapshot bundling the cached list and any derived structures.</typeparam>
    internal abstract class ReferenceCacheHolder<TSnapshot>(IServiceScopeFactory scopeFactory)
        : IReloadableReferenceCache
        where TSnapshot : class
    {
        // Serializes reloads on this holder so concurrent callers never race to publish competing snapshots.
        // Serializing (rather than coalescing onto an in-flight reload) is what preserves read-your-writes:
        // an awaited reload always starts a fresh query after acquiring the gate, so it observes the caller's
        // just-committed write rather than the result of a load that began before it.
        private readonly SemaphoreSlim _reloadGate = new(1, 1);

        // Published via Volatile.Read/Write rather than the `volatile` keyword, which C# does not allow on a
        // field of a generic type parameter. The class constraint makes this a reference, so the swap is a
        // single atomic assignment and the volatile access provides the cross-thread visibility.
        private TSnapshot? _current;

        /// <summary>
        /// The current immutable snapshot. Populated at startup (and on every admin write) before traffic is
        /// served; throws if accessed before the first load rather than returning a torn or empty default.
        /// </summary>
        public TSnapshot Current => Volatile.Read(ref _current)
            ?? throw new InvalidOperationException(
                $"{GetType().Name} has not been loaded. Reference caches must be initialized at startup before any read.");

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            await _reloadGate.WaitAsync(cancellationToken);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var snapshot = await BuildSnapshotAsync(context, cancellationToken);
                Volatile.Write(ref _current, snapshot);
            }
            finally
            {
                _reloadGate.Release();
            }
        }

        /// <summary>Builds the complete new snapshot from the database, off to the side, before it is published.</summary>
        protected abstract Task<TSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken);
    }
}
