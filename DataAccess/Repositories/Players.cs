using GameCore.DataAccess;
using GameCore.Entities;
using GameInfrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories
{
    internal class Players : BaseRepository, IPlayers
    {
        public static readonly object _lock = new();
        public static bool _processingQueue = false;

        public Players(GameContext database) : base(database) { }

        public async Task<Player?> GetPlayerByUserNameAsync(string userName)
        {
            return await Database.Players
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .FirstOrDefaultAsync();
        }
    }
}
