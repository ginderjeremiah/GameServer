using GameCore.DataAccess;
using GameCore.Entities;
using GameCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public static readonly object _lock = new();
        public static bool _processingQueue = false;

        public Players(IDatabaseService database) : base(database) { }

        public async Task<Player?> GetPlayerByUserNameAsync(string userName)
        {
            return await Database.Players
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .FirstOrDefaultAsync();
        }

        public async Task SavePlayerAsync(Player player)
        {
            Database.Update(player);
            await Database.SaveChangesAsync();
            Database.Untrack(player);
        }
    }
}
