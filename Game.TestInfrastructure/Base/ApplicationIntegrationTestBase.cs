using Game.Application.DependencyInjection;
using Game.Core.Events;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.TestInfrastructure.Base
{
    public abstract class ApplicationIntegrationTestBase : IAsyncLifetime
    {
        private ServiceProvider? _rootProvider;
        private ITestOutputHelper _testOutputHelper;

        protected IntegrationTestContainers Containers { get; }
        protected CancellationToken CancellationToken => TestContext.Current.CancellationToken;

        protected ApplicationIntegrationTestBase(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            Containers = containers;
        }

        public async ValueTask InitializeAsync()
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.ClearProviders().AddProvider(new XunitLoggerProvider(_testOutputHelper)));

            services.AddSingleton(
                Options.Create(new DataAccessOptions
                {
                    DatabaseSystem = Abstractions.Infrastructure.DatabaseSystem.Postgres,
                    DbConnectionString = Containers.DbConnectionString,
                    CacheSystem = Abstractions.Infrastructure.CacheSystem.Redis,
                    CacheConnectionString = Containers.CacheConnectionString,
                    PubSubSystem = Abstractions.Infrastructure.PubSubSystem.Redis,
                    PubSubConnectionString = Containers.PubSubConnectionString,
                }));

            services.AddDataAccess();
            services.AddDomainEventDispatcher();
            services.AddApplication();

            // Remove all hosted services (DataProviderSynchronizer) — no async background processing in tests
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();

            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            _rootProvider = services.BuildServiceProvider();

            await CleanupAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_rootProvider is not null)
            {
                await _rootProvider.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }

        protected IServiceScope CreateScope() => _rootProvider!.CreateScope();

        /// <summary>
        /// Rebuilds the reference-data cache snapshots from the current database state. Call after seeding
        /// reference rows directly so the caches (which no longer lazily refill) serve the seeded data.
        /// </summary>
        protected Task ReloadReferenceCachesAsync() => ReferenceCacheReloader.ReloadAllAsync(_rootProvider!);

        protected async Task CleanupAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await DatabaseCleaner.TruncatePlayerDataAsync(context);
            await RedisCleaner.FlushAsync(Containers.CacheConnectionString);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);
        }
    }
}
