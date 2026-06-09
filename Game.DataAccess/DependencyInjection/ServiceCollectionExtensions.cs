using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Game.Application;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess.Repositories;
using Game.DataAccess.Repositories.Admin;
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
            RegisterPlayerPersistenceHandlers();

            return services.AddTransient<InfrastructureOptions>(sp => sp.GetRequiredService<IOptions<DataAccessOptions>>().Value)
                .AddGameContext()
                .AddCache()
                .AddPubSub()
                .AddSingleton(PlayerUpdateRetryPolicy.Default)
                .AddSingleton<DataProviderSynchronizer>()
                .AddHostedService(sp => sp.GetRequiredService<DataProviderSynchronizer>())
                // Player aggregate (write-behind: Redis + async sync)
                .AddScoped<IPlayerRepository, PlayerRepository>()
                // Entity store (admin tools)
                .AddScoped<IEntityStore, EntityStore>()
                // UnitOfWork (stats/challenges persistence)
                .AddScoped<IUnitOfWork, UnitOfWork>()
                // Player progress repo (UnitOfWork saves; also serves read-only API queries)
                .AddScoped<IPlayerProgressRepository, PlayerProgressRepository>()
                // Reference data repos (in-memory cached). Each serves both its public read contract and
                // an internal entity-cache/queries seam (used by Enemies to build domain enemies, and by
                // the Content Authoring admin repositories for existence/diff lookups); a single scoped
                // instance backs both so the cached entity list is shared rather than duplicated.
                .AddScoped<IChallenges, Challenges>()
                .AddScoped<Enemies>()
                .AddScoped<IEnemies>(sp => sp.GetRequiredService<Enemies>())
                .AddScoped<IEnemyEntityCache>(sp => sp.GetRequiredService<Enemies>())
                .AddScoped<Items>()
                .AddScoped<IItems>(sp => sp.GetRequiredService<Items>())
                .AddScoped<IItemEntityCache>(sp => sp.GetRequiredService<Items>())
                .AddScoped<ItemMods>()
                .AddScoped<IItemMods>(sp => sp.GetRequiredService<ItemMods>())
                .AddScoped<IItemModEntityCache>(sp => sp.GetRequiredService<ItemMods>())
                .AddScoped<IItemModTypes, ItemModTypes>()
                .AddScoped<IItemCategories, ItemCategories>()
                .AddScoped<Skills>()
                .AddScoped<ISkills>(sp => sp.GetRequiredService<Skills>())
                .AddScoped<ISkillEntityCache>(sp => sp.GetRequiredService<Skills>())
                .AddScoped<Tags>()
                .AddScoped<ITags>(sp => sp.GetRequiredService<Tags>())
                .AddScoped<ITagEntityQueries>(sp => sp.GetRequiredService<Tags>())
                .AddScoped<ITagCategories, TagCategories>()
                .AddScoped<Zones>()
                .AddScoped<IZones>(sp => sp.GetRequiredService<Zones>())
                .AddScoped<IZoneEntityCache>(sp => sp.GetRequiredService<Zones>())
                .AddScoped<ISessionStore, SessionStore>()
                .AddScoped<IRefreshTokenStore, RefreshTokenStore>()
                .AddScoped<IUsers, Users>()
                .AddScoped<IUserLogins, UserLogins>()
                .AddScoped<IRoles, Roles>()
                // Admin "Content Authoring" persistence (entity-free seam for the admin controllers)
                .AddScoped<IAdminEnemies, AdminEnemies>()
                .AddScoped<IAdminItems, AdminItems>()
                .AddScoped<IAdminItemMods, AdminItemMods>()
                .AddScoped<IAdminSkills, AdminSkills>()
                .AddScoped<IAdminZones, AdminZones>()
                .AddScoped<IAdminChallenges, AdminChallenges>()
                .AddScoped<IAdminTags, AdminTags>();
        }

        /// <summary>
        /// Registers <see cref="PlayerPersistencePublisher"/> against each player domain event whose
        /// change must be written behind to the database. Events that are only relevant in-process
        /// (e.g. <see cref="PlayerLeveledUpEvent"/>) are intentionally omitted so they are never
        /// published to the persistence queue.
        /// </summary>
        private static void RegisterPlayerPersistenceHandlers()
        {
            DomainEventDispatcher.RegisterDomainEventHandler<PlayerCoreUpdatedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<AttributeAllocationsChangedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ItemUnlockedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ItemEquippedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ItemUnequippedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ModUnlockedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ModAppliedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ModRemovedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<ItemFavoriteChangedEvent, PlayerPersistencePublisher>();
            DomainEventDispatcher.RegisterDomainEventHandler<LogPreferenceChangedEvent, PlayerPersistencePublisher>();
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
