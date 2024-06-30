using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameInfrastructure.Database
{
    internal class GameContextDesignTimeFactory : IDesignTimeDbContextFactory<GameContext>
    {
        public GameContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            optionsBuilder.UseSqlServer("Data Source=JEREMIAH-PC;Database=GameNew;Integrated Security=true;TrustServerCertificate=true");

            return new GameContext(optionsBuilder.Options);
        }
    }
}
