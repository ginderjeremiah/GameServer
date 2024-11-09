using Game.Infrastructure.Cache;
using Game.Infrastructure.Database;
using Game.Infrastructure.PubSub;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding infrastructure services to an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a <see cref="GameContext"/> to the <see cref="IServiceCollection"/>.  Requires a configured <see cref="InfrastructureOptions"/> service
        /// to be registered in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddGameContext(this IServiceCollection services)
        {
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                return GameContextFactory.GetGameContext(options);
            });
        }

        /// <summary>
        /// Adds a <see cref="Core.Infrastructure.ICacheService"/> to the <see cref="IServiceCollection"/>.  Requires a configured <see cref="InfrastructureOptions"/> service
        /// to be registered in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddCache(this IServiceCollection services)
        {
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return CacheServiceFactory.GetCacheService(options, loggerFactory);
            });
        }

        /// <summary>
        /// Adds a <see cref="Core.Infrastructure.IPubSubService"/> to the <see cref="IServiceCollection"/>.  Requires a configured <see cref="InfrastructureOptions"/> service
        /// to be registered in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
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
