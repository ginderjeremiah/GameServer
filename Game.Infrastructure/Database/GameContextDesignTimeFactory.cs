using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Game.Infrastructure.Database
{
    internal class GameContextDesignTimeFactory : IDesignTimeDbContextFactory<GameContext>
    {
        public GameContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GameContext>();
            optionsBuilder.UseNpgsql("Server=localhost;User Id=postgres;Database=Game");
            return new GameContext(optionsBuilder.Options);
        }
    }
}
