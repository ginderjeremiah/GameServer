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
                case SqlServer:
                default:
                    optionsBuilder.UseSqlServer(config.DbConnectionString)
                        .EnableSensitiveDataLogging(config.EnableSensitiveLogging);
                    break;
            }

            return new GameContext(optionsBuilder.Options);
        }
    }
}
