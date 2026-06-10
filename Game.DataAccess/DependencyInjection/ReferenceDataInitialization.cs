using Game.Abstractions.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.DependencyInjection
{
    /// <summary>
    /// Startup initialization for the in-memory reference-data caches.
    /// </summary>
    public static class ReferenceDataInitialization
    {
        /// <summary>
        /// Eagerly loads every cached reference-data set before the application serves traffic. Each
        /// cached reference repo fills its static list on first read; invoking those reads here triggers
        /// the fill up front, so a database problem surfaces as a boot failure rather than on the first
        /// player request. Resolving the repos in a scope and invoking their cached reads is sufficient
        /// while the lazy-fill path still exists (#357); the reload-and-swap follow-up (#358) will replace
        /// the internals with an awaited reload over the DI-discovered cache set.
        /// </summary>
        public static void InitializeReferenceCaches(this IServiceProvider provider)
        {
            // Reference repos are scoped, so resolve and read them within a dedicated scope.
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            _ = scopedProvider.GetRequiredService<IItems>().All();
            _ = scopedProvider.GetRequiredService<IItemMods>().All();
            _ = scopedProvider.GetRequiredService<ISkills>().AllSkills();
            _ = scopedProvider.GetRequiredService<IEnemies>().All();
            _ = scopedProvider.GetRequiredService<IZones>().All();
            _ = scopedProvider.GetRequiredService<IChallenges>().All();
        }
    }
}
