using Game.Application.Events;
using Game.Application.Services;
using Game.Core.Battle.Events;
using Game.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            RegisterDomainEventHandlers();
            return services
                .AddScoped<AccountService>()
                .AddScoped<BattleSnapshotService>()
                .AddScoped<BattleService>()
                .AddScoped<LoginTrackingService>()
                .AddScoped<PlayerService>();
        }

        public static void RegisterDomainEventHandlers()
        {
            DomainEventDispatcher.RegisterDomainEventHandler<IDomainEvent, LoggingEventHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<BattleCompletedEvent, BattleStatisticsEventHandler>();
        }
    }
}
