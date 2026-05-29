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
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

        public string ConnectionString => _container.GetConnectionString();

        public async Task StartAsync()
        {
            await _container.StartAsync();
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
            await _container.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
