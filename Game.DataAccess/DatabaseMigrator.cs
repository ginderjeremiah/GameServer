using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Game.DataAccess
{
    internal class DatabaseMigrator(GameContext context, ILogger<DatabaseMigrator> logger) : IDatabaseMigrator
    {
        public async Task Migrate(bool resetDatabase = false)
        {
            var start = Stopwatch.GetTimestamp();
            logger.LogDebug($"Beginning migration.");

            await context.Database.MigrateAsync();

            logger.LogDebug("Finished migration. Elapsed time {ElapsedTime}", Stopwatch.GetElapsedTime(start));
        }
    }
}
