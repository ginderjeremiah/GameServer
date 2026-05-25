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
                    optionsBuilder.UseNpgsql(config.DbConnectionString)
                        .EnableSensitiveDataLogging();
                    break;
                case SqlServer:
                default:
                    optionsBuilder.UseSqlServer(config.DbConnectionString)
                        .EnableSensitiveDataLogging();
                    break;
            }

            return new GameContext(optionsBuilder.Options);
        }
    }
}
