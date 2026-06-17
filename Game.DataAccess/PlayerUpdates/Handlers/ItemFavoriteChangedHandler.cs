using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemFavoriteChangedHandler(GameContext context) : IPlayerUpdateHandler<ItemFavoriteChangedEvent>
    {
        public async Task HandleAsync(ItemFavoriteChangedEvent evt)
        {
            // Idempotent absolute update: set the flag to the event's value for the player's unlocked item.
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.Favorite, evt.Favorite));
        }
    }
}
