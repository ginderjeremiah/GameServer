using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModUnlockedHandler(GameContext context) : IPlayerUpdateHandler<ModUnlockedEvent>
    {
        public async Task HandleAsync(ModUnlockedEvent evt)
        {
            var exists = await context.UnlockedMods
                .AnyAsync(um => um.PlayerId == evt.PlayerId && um.ItemModId == evt.ItemModId);

            if (!exists)
            {
                context.UnlockedMods.Add(new UnlockedMod
                {
                    PlayerId = evt.PlayerId,
                    ItemModId = evt.ItemModId,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
