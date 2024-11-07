using Microsoft.EntityFrameworkCore;
using static Game.Core.Infrastructure.DatabaseSystem;

namespace Game.Infrastructure.Database
{
    internal static class GameContextFactory
    {
        public static GameContext GetGameContext(IDatabaseOptions config)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            switch (config.DatabaseSystem)
            {
                case SqlServer:
                default:
                    optionsBuilder.UseSqlServer(config.DbConnectionString);
                    break;

            }

            return new GameContext(optionsBuilder.Options);
        }
    }
}
