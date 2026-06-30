using Game.Abstractions.Auth;
using Game.Application.Auth;
using Game.Application.Content;
using Game.Application.Events;
using Game.Application.Services;
using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Battle.Offline;
using Game.Core.Events;
using Game.Core.Players;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            RegisterDomainEventHandlers();
            return services
                // BattleFactory, NewPlayerFactory and OfflineProgressSimulator are stateless domain
                // services with no out-of-process dependencies, so they are shared.
                .AddSingleton<BattleFactory>()
                .AddSingleton<NewPlayerFactory>()
                .AddSingleton<OfflineProgressSimulator>()
                // Stateless (parameters come from injected options), so a single shared instance.
                .AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>()
                // Per-account login backoff: a stateless policy (shared) plus a scoped guard over the
                // scoped Redis-backed store. The system clock is injected (TimeProvider) so the time-based
                // backoff math stays deterministically testable.
                .AddSingleton(TimeProvider.System)
                .AddSingleton<LoginBackoffPolicy>()
                .AddScoped<LoginBackoffGuard>()
                .AddScoped<AccountService>()
                .AddScoped<BattleService>()
                .AddScoped<ChallengeRewardService>()
                .AddScoped<ProficiencyRewardService>()
                .AddScoped<LoginTrackingService>()
                .AddScoped<PlayerService>()
                .AddScoped<SynthesisService>()
                // Reads the reference caches (scoped repos) to mirror the static content graph to JSON.
                .AddScoped<IContentExporter, ContentExporter>();
        }

        public static void RegisterDomainEventHandlers()
        {
            DomainEventDispatcher.RegisterDomainEventHandler<IDomainEvent, LoggingEventHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<BattleCompletedEvent, BattleStatisticsEventHandler>();
        }
    }
}
