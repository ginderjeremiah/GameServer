using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Game.Infrastructure.Database
{
    internal class GameContextDesignTimeFactory : IDesignTimeDbContextFactory<GameContext>
    {
        // Used only by the EF tooling at design time (Add-Migration / Update-Database). The connection string
        // falls back to an env var so design-time migrations work across environments whose Postgres isn't the
        // hardcoded local default (credential-less localhost), without baking real credentials into source.
        private const string ConnectionStringEnvVar = "GAMESERVER_DESIGN_TIME_CONNECTION";
        private const string DefaultConnectionString = "Server=localhost;User Id=postgres;Database=Game";

        public GameContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = DefaultConnectionString;
            }

            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new GameContext(optionsBuilder.Options);
        }
    }
}
