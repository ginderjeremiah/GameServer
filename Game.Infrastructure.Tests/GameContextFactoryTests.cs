using Game.Abstractions.Infrastructure;
using Game.Infrastructure.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Tests that <see cref="GameContextFactory"/> maps the configured <see cref="DatabaseSystem"/> to a
    /// provider and fails loud on an unset/unsupported value rather than silently defaulting (#453). Building
    /// the <c>DbContextOptions</c> never opens a connection, so these stay in-process unit tests with no
    /// out-of-process dependency.
    /// </summary>
    public class GameContextFactoryTests
    {
        [Fact]
        public void GetGameContext_ForPostgres_ConfiguresNpgsqlProvider()
        {
            var options = new InfrastructureOptions
            {
                DatabaseSystem = DatabaseSystem.Postgres,
                DbConnectionString = "Host=localhost;Database=Game"
            };

            using var context = GameContextFactory.GetGameContext(options, NullLoggerFactory.Instance);

            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        }

        [Fact]
        public void GetGameContext_ForPostgres_ConfiguresANonRetryingExecutionStrategy()
        {
            var options = new InfrastructureOptions
            {
                DatabaseSystem = DatabaseSystem.Postgres,
                DbConnectionString = "Host=localhost;Database=Game"
            };

            using var context = GameContextFactory.GetGameContext(options, NullLoggerFactory.Instance);

            // Not a style preference: PlayerWriteWatermarkGuard opens a transaction around every guarded
            // write-behind apply, and a retrying execution strategy refuses user-initiated transactions
            // outright. Enabling EnableRetryOnFailure without first wrapping the guard in
            // CreateExecutionStrategy().ExecuteAsync(...) would throw on every guarded apply — a coupling that
            // was implicit until #2497, so it is pinned here rather than left to a comment.
            Assert.False(
                context.Database.CreateExecutionStrategy().RetriesOnFailure,
                "A retrying execution strategy refuses the transaction PlayerWriteWatermarkGuard opens around "
                + "every guarded write-behind apply. Wrap the guard in CreateExecutionStrategy().ExecuteAsync "
                + "before enabling retries.");
        }

        [Fact]
        public void GetGameContext_ForUnsetDatabaseSystem_ThrowsInvalidOperationException()
        {
            // An unset/missing config binds to the enum's default (0), which has no member: the factory must
            // reject it loudly instead of selecting a provider the app cannot actually use.
            var options = new InfrastructureOptions
            {
                DatabaseSystem = default,
                DbConnectionString = "Host=localhost;Database=Game"
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => GameContextFactory.GetGameContext(options, NullLoggerFactory.Instance));
            Assert.Contains("DatabaseSystem", ex.Message);
        }

        [Fact]
        public void GetGameContext_ForUnknownDatabaseSystem_ThrowsInvalidOperationException()
        {
            var options = new InfrastructureOptions
            {
                DatabaseSystem = (DatabaseSystem)99,
                DbConnectionString = "Host=localhost;Database=Game"
            };

            Assert.Throws<InvalidOperationException>(
                () => GameContextFactory.GetGameContext(options, NullLoggerFactory.Instance));
        }
    }
}
