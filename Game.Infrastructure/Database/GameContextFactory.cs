using Microsoft.EntityFrameworkCore;
using static Game.Abstractions.Infrastructure.DatabaseSystem;

namespace Game.Infrastructure.Database
{
    internal static class GameContextFactory
    {
        public static GameContext GetGameContext(IDatabaseOptions config)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            switch (config.DatabaseSystem)
            {
                case Postgres:
                    optionsBuilder.UseNpgsql(config.DbConnectionString);
                    break;
                case SqlServer:
                default:
                    optionsBuilder.UseSqlServer(config.DbConnectionString);
                    break;
            }

            return new GameContext(optionsBuilder.Options);
        }
    }
}
