using Game.Abstractions.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Game.TestInfrastructure.Helpers
{
    public static class ReferenceCacheCleaner
    {
        public static void InvalidateAll(IServiceProvider provider)
        {
            foreach (var cache in provider.GetServices<ICacheInvalidatable>())
            {
                cache.InvalidateCache();
            }
        }
    }
}
