using Game.Abstractions.DataAccess;
using Game.DataAccess;
using Game.DataAccess.DependencyInjection;
using Game.Infrastructure;
using Game.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Game.TestInfrastructure.Fixtures
{
    public class PostgresContainerFixture : IAsyncDisposable
    {
        private readonly PreexistingContainerInfo? _preexisting = PreexistingContainerInfo.TryLoad();

        // Only provision a Testcontainers-managed container when no pre-existing PostgreSQL was
        // supplied by the session-start hook (see PreexistingContainerInfo).
        private readonly PostgreSqlContainer? _container;

        public PostgresContainerFixture()
        {
            _container = _preexisting is null
                ? new PostgreSqlBuilder("postgres:18-alpine").Build()
                : null;
        }

        public string ConnectionString => _preexisting?.Postgres ?? _container!.GetConnectionString();

        public async Task StartAsync()
        {
            if (_container is not null)
            {
                await _container.StartAsync();
            }

            // Migrations are applied in both modes: a freshly started container is empty, and a
            // reused container relies on EF's idempotent migrations to converge to the schema.
            await ApplyMigrationsAsync();
        }

        private async Task ApplyMigrationsAsync()
        {
            var services = new ServiceCollection();

            services.AddTransient<InfrastructureOptions>(_ => new DataAccessOptions
            {
                DatabaseSystem = Abstractions.Infrastructure.DatabaseSystem.Postgres,
                DbConnectionString = ConnectionString,
                EnableSensitiveLogging = true,
            });

            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
            services.AddLogging();
            services.AddGameContext();
            services.AddDatabaseMigrator();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
            await migrator.Migrate();
        }

        public async ValueTask DisposeAsync()
        {
            // A reused container is owned by the session-start hook, not the test process.
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }
    }
}
