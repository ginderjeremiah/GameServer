using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Game.Abstractions.Infrastructure.DatabaseSystem;

namespace Game.Infrastructure.Database
{
    internal static class GameContextFactory
    {
        public static GameContext GetGameContext(IDatabaseOptions config, ILoggerFactory loggerFactory)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            Configure(optionsBuilder, config, loggerFactory);
            return new GameContext(optionsBuilder.Options);
        }

        // Shared with the DI registration (AddDbContextPool), which builds its DbContextOptions once at
        // startup rather than per resolution/pooled-instance rent.
        internal static void Configure(DbContextOptionsBuilder optionsBuilder, IDatabaseOptions config, ILoggerFactory loggerFactory)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);

            switch (config.DatabaseSystem)
            {
                case Postgres:
                    var connectionString = config.DbConnectionString;
                    if (config.EnableSensitiveLogging)
                    {
                        connectionString = $"{connectionString};Include Error Detail=True";
                    }

                    optionsBuilder.UseNpgsql(connectionString)
                        .EnableSensitiveDataLogging(config.EnableSensitiveLogging);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported DatabaseSystem '{config.DatabaseSystem}'. The application only supports "
                        + $"{nameof(Postgres)} (DataAccessOptions:DatabaseSystem = {(int)Postgres}); a missing or "
                        + "unrecognized value is rejected rather than defaulted because there is no other supported provider.");
            }
        }
    }
}
