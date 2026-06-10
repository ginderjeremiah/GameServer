using Game.DataAccess.DependencyInjection;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Reloads every reference-data cache from the database — the test-side entry point onto the same
    /// build-then-swap reload the app runs at startup. Tests call it after seeding reference rows directly
    /// (bypassing the admin write path that would otherwise reload) so the cached snapshots pick up what was
    /// just seeded, and the test bases call it between tests to reset the holders to the (truncated) database
    /// state. Delegates to the production <see cref="ReferenceDataInitialization.InitializeReferenceCachesAsync"/>
    /// so the test path exercises the exact code the app runs.
    /// </summary>
    public static class ReferenceCacheReloader
    {
        public static Task ReloadAllAsync(IServiceProvider provider) => provider.InitializeReferenceCachesAsync();
    }
}
