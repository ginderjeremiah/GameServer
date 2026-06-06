using Game.Abstractions.DataAccess;
using Game.Application;
using Game.DataAccess.Repositories;
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
        public static IServiceCollection AddDataAccess(this IServiceCollection services)
        {
            return services.AddTransient<InfrastructureOptions>(sp => sp.GetRequiredService<IOptions<DataAccessOptions>>().Value)
                .AddGameContext()
                .AddCache()
                .AddPubSub()
                .AddSingleton<DataProviderSynchronizer>()
                .AddHostedService(sp => sp.GetRequiredService<DataProviderSynchronizer>())
                // Player aggregate (write-behind: Redis + async sync)
                .AddScoped<IPlayerRepository, PlayerRepository>()
                // Entity store (admin tools)
                .AddScoped<IEntityStore, EntityStore>()
                // UnitOfWork (stats/challenges persistence)
                .AddScoped<IUnitOfWork, UnitOfWork>()
                // Player progress aggregate repo (UnitOfWork saves)
                .AddScoped<IPlayerProgressRepository, PlayerProgressRepository>()
                // Read-only repos for API queries
                .AddScoped<IPlayerStatistics, PlayerStatistics>()
                .AddScoped<IPlayerChallenges, PlayerChallenges>()
                // Reference data repos (in-memory cached)
                .AddScoped<IChallenges, Challenges>()
                .AddScoped<IEnemies, Enemies>()
                .AddScoped<IItems, Items>()
                .AddScoped<IItemMods, ItemMods>()
                .AddScoped<IItemModTypes, ItemModTypes>()
                .AddScoped<IItemCategories, ItemCategories>()
                .AddScoped<ISkills, Skills>()
                .AddScoped<ITags, Tags>()
                .AddScoped<ITagCategories, TagCategories>()
                .AddScoped<IZones, Zones>()
                .AddScoped<ISessionStore, SessionStore>()
                .AddScoped<IRefreshTokenStore, RefreshTokenStore>()
                .AddScoped<IUsers, Users>()
                .AddScoped<IUserLogins, UserLogins>()
                .AddScoped<IRoles, Roles>();
        }

        /// <summary>
        /// Adds an <see cref="IDatabaseMigrator"/> to the <see cref="IServiceCollection"/>.
        /// </summary>
        public static IServiceCollection AddDatabaseMigrator(this IServiceCollection services)
        {
            return services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();
        }
    }
}
