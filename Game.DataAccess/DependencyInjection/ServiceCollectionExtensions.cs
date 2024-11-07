using Game.Core.DataAccess;
using Game.Infrastructure;
using Game.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Game.DataAccess.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
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

        public static IServiceCollection AddRepositoryManager(this IServiceCollection services)
        {
            return services.AddTransient<InfrastructureOptions>(sp => sp.GetRequiredService<IOptions<DataAccessOptions>>().Value)
                .AddGameContext()
                .AddCache()
                .AddPubSub()
                .AddSingleton<DataProviderSynchronizer>()
                .AddTransient<IRepositoryManager, RepositoryManager>();
        }

        public static IServiceCollection AddDatabaseMigrator(this IServiceCollection services)
        {
            return services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();
        }
    }
}
