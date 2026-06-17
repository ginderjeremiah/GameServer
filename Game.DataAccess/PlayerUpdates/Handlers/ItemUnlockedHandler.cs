using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemUnlockedHandler(GameContext context) : IPlayerUpdateHandler<ItemUnlockedEvent>
    {
        public async Task HandleAsync(ItemUnlockedEvent evt)
        {
            var exists = await context.UnlockedItems
                .AnyAsync(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId);

            if (!exists)
            {
                context.UnlockedItems.Add(new UnlockedItem
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
