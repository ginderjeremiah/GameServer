using Game.Infrastructure.Cache;
using Game.Infrastructure.Database;
using Game.Infrastructure.PubSub;
using Game.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding infrastructure services to an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a pooled <see cref="GameContext"/> to the <see cref="IServiceCollection"/>.  Requires a configured
        /// <see cref="InfrastructureOptions"/> service to be registered in the <see cref="IServiceCollection"/>.
        /// The options builder runs once at startup (not per resolution), and <see cref="GameContext"/> instances
        /// are rented from a pool rather than allocated fresh — cheaper on the per-scope resolution path
        /// (including the drain-loop's scope-per-event pattern) since <c>GameContext</c> has no per-scope state
        /// beyond the pooled options.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddGameContext(this IServiceCollection services)
        {
            return services.AddDbContextPool<GameContext>((sp, optionsBuilder) =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                GameContextFactory.Configure(optionsBuilder, options, loggerFactory);
            });
        }

        /// <summary>
        /// Adds a <see cref="Core.Infrastructure.ICacheService"/> to the <see cref="IServiceCollection"/>.  Requires a configured <see cref="InfrastructureOptions"/> service
        /// to be registered in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddCache(this IServiceCollection services)
        {
            services.AddRedisConnectionLifetime();
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
            services.AddRedisConnectionLifetime();
            return services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<InfrastructureOptions>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return PubSubServiceFactory.GetPubSubService(options, loggerFactory);
            });
        }

        // Registers the graceful-shutdown hook that disposes the shared Redis multiplexers on host stop (#954).
        // TryAddEnumerable dedupes by implementation type, so calling this from both AddCache and AddPubSub
        // registers the single hosted service exactly once however the infrastructure services are wired.
        // Registering it here — ahead of the Redis-consuming hosted services (the write-behind synchronizer, the
        // reference-cache synchronizer, the socket registry) — means it stops last (hosted services stop in
        // reverse registration order), so the connections are torn down only after those consumers have drained.
        private static void AddRedisConnectionLifetime(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RedisConnectionLifetime>());
        }
    }
}
