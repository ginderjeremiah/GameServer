using Game.Core.DataAccess;
using Game.Infrastructure;
using Game.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Game.DataAccess.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding data access services to an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {

        /// <summary>
        /// Adds an <see cref="IRepositoryManager"/> to the <see cref="IServiceCollection"/>.  Requires an already registered 
        /// <see cref="IOptions{TOptions}"/> for <see cref="DataAccessOptions"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddRepositoryManager(this IServiceCollection services)
        {
            return services.AddTransient<InfrastructureOptions>(sp => sp.GetRequiredService<IOptions<DataAccessOptions>>().Value)
                .AddGameContext()
                .AddCache()
                .AddPubSub()
                .AddSingleton<DataProviderSynchronizer>()
                .AddTransient<IRepositoryManager, RepositoryManager>();
        }

        /// <summary>
        /// Adds an <see cref="IRepositoryManager"/> to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        /// <param name="configureOptions"> A delegate used to configure the <see cref="DataAccessOptions"/></param>
        public static IServiceCollection AddRepositoryManager(this IServiceCollection services, Action<DataAccessOptions> configureOptions)
        {
            var options = new DataAccessOptions();
            configureOptions(options);
            return services.AddTransient<InfrastructureOptions>((sp) => options)
                .AddGameContext()
                .AddCache()
                .AddPubSub()
                .AddSingleton<DataProviderSynchronizer>();
        }


        /// <summary>
        /// Adds an <see cref="IDatabaseMigrator"/> to the <see cref="IServiceCollection"/>.  Requires an <see cref="IRepositoryManager"/> to be registered
        /// to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddDatabaseMigrator(this IServiceCollection services)
        {
            return services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();
        }
    }
}
