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

        public async Task SavePlayerAsync(Player player, List<PlayerAttribute> attributes)
        {
            //var usedAtts = new List<PlayerAttribute>();

            //await Database.Players.SelectMany(p => p.PlayerAttributes).ForEachAsync(att =>
            //{
            //    var match = attributes.FirstOrDefault(attribute => attribute.AttributeId == att.AttributeId);
            //    if (match is null)
            //    {
            //        Database.Delete(att);
            //    }
            //    else
            //    {
            //        att.Amount = match.Amount;
            //        Database.Update(att);
            //        usedAtts.Add(att);
            //    }
            //});

            //foreach (var att in attributes.Except(usedAtts))
            //{

            //}

            player.PlayerAttributes = attributes;
            Database.Update(player);

            await Database.SaveChangesAsync();
        }
    }
}
