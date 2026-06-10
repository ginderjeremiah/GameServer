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
        /// Eagerly loads every reference-data cache before the application serves traffic by reloading each
        /// DI-discovered <see cref="IReloadableReferenceCache"/> (the singleton snapshot holders). A database
        /// problem therefore surfaces as a boot failure rather than on the first player request, and once
        /// this completes every holder has a published snapshot so reads never observe an empty cache.
        /// </summary>
        public static async Task InitializeReferenceCachesAsync(this IServiceProvider provider, CancellationToken cancellationToken = default)
        {
            // The holders are singletons and create their own scope for the load, so no outer scope is needed.
            foreach (var cache in provider.GetServices<IReloadableReferenceCache>())
            {
                await cache.ReloadAsync(cancellationToken);
            }
        }
    }
}
