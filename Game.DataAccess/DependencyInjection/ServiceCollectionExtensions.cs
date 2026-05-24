using Game.Abstractions.DataAccess;
using Game.Application;
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
        /// Adds all data-access services to the <see cref="IServiceCollection"/>.  Requires an already registered
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
                // RepositoryManager is Scoped so all focused-interface registrations share
                // the same instance (and the same EF Core DbContext) within a request.
                .AddScoped<RepositoryManager>()
                .AddScoped<IPlayerRepository>(sp => sp.GetRequiredService<RepositoryManager>())
                .AddScoped<IWorldRepository>(sp => sp.GetRequiredService<RepositoryManager>())
                .AddScoped<IEntityStore>(sp => sp.GetRequiredService<RepositoryManager>())
                // Individual sub-repository interfaces resolved through the shared RepositoryManager.
                .AddScoped<IPlayers>(sp => sp.GetRequiredService<RepositoryManager>().Players)
                .AddScoped<ISessionStore>(sp => sp.GetRequiredService<RepositoryManager>().SessionStore)
                .AddScoped<IEnemies>(sp => sp.GetRequiredService<RepositoryManager>().Enemies)
                .AddScoped<IZones>(sp => sp.GetRequiredService<RepositoryManager>().Zones)
                .AddScoped<IItems>(sp => sp.GetRequiredService<RepositoryManager>().Items)
                .AddScoped<ISkills>(sp => sp.GetRequiredService<RepositoryManager>().Skills)
                .AddScoped<IAttributes>(sp => sp.GetRequiredService<RepositoryManager>().Attributes)
                .AddScoped<IItemMods>(sp => sp.GetRequiredService<RepositoryManager>().ItemMods)
                .AddScoped<IItemModTypes>(sp => sp.GetRequiredService<RepositoryManager>().ItemModTypes)
                .AddScoped<IItemCategories>(sp => sp.GetRequiredService<RepositoryManager>().ItemCategories)
                .AddScoped<ITags>(sp => sp.GetRequiredService<RepositoryManager>().Tags)
                .AddScoped<ITagCategories>(sp => sp.GetRequiredService<RepositoryManager>().TagCategories)
                .AddScoped<IUsers>(sp => sp.GetRequiredService<RepositoryManager>().Users)
                .AddScoped<IUnitOfWork, UnitOfWork>();
        }

        /// <summary>
        /// Adds infrastructure services to the <see cref="IServiceCollection"/> using the provided options.
        /// Note: this overload does not register repositories — use <see cref="AddRepositoryManager(IServiceCollection)"/> for the full registration.
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
        /// Adds an <see cref="IDatabaseMigrator"/> to the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services"> The dependency injection container to configure.</param>
        public static IServiceCollection AddDatabaseMigrator(this IServiceCollection services)
        {
            return services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();
        }
    }
}
