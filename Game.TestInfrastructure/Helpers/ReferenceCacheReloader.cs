using Game.Abstractions.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Reloads every reference-data cache from the database — the test-side entry point onto the same
    /// build-then-swap <see cref="IReloadableReferenceCache.ReloadAsync"/> the app uses. Tests call it after
    /// seeding reference rows directly (bypassing the admin write path that would otherwise reload) so the
    /// cached snapshots pick up what was just seeded, and the test bases call it between tests to reset the
    /// holders to the (truncated) database state.
    /// </summary>
    public static class ReferenceCacheReloader
    {
        public static async Task ReloadAllAsync(IServiceProvider provider)
        {
            foreach (var cache in provider.GetServices<IReloadableReferenceCache>())
            {
                await cache.ReloadAsync();
            }
        }
    }
}
