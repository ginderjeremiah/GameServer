using Microsoft.EntityFrameworkCore;
using static GameInfrastructure.DatabaseSystem;

namespace GameInfrastructure.Database
{
    public static class GameContextFactory
    {
        public static GameContext GetGameContext(IDatabaseConfiguration config)
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
