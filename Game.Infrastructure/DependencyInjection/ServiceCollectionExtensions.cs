using Game.Infrastructure.Cache;
using Game.Infrastructure.Database;
using Game.Infrastructure.PubSub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGameContext(this IServiceCollection services)
        {
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                return GameContextFactory.GetGameContext(options);
            });
        }

        public static IServiceCollection AddCache(this IServiceCollection services)
        {
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return CacheServiceFactory.GetCacheService(options, loggerFactory);
            });
        }

        public static IServiceCollection AddPubSub(this IServiceCollection services)
        {
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return PubSubServiceFactory.GetPubSubService(options, loggerFactory);
            });
        }
    }
}
