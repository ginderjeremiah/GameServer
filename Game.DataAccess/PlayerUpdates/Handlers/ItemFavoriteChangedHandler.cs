using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemFavoriteChangedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ItemFavoriteChangedEvent>
    {
        // Keyed per item — see PlayerWriteStream.ItemFavorite for why that granularity is the contract.
        public Task HandleAsync(ItemFavoriteChangedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.ItemFavorite,
                [PlayerWriteWatermarkGuard.Target(evt.ItemId)],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, ItemFavoriteChangedEvent evt)
        {
            // Absolute upsert of the favorite flag. A favorite presupposes ownership, so a missing row means the
            // item's ItemUnlockedEvent was reordered behind this event under best-effort cross-instance ordering
            // — insert the row carrying the flag (and a null slot) rather than ExecuteUpdate's silent zero-row
            // no-op dropping the favorite until a later edit self-heals the DB. The later unlock then no-ops on
            // the existing row. Idempotent: re-applying converges to the same flag value.
            var row = await context.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId);

            if (row is null)
            {
                context.UnlockedItems.Add(new UnlockedItem
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                    Favorite = evt.Favorite,
                });
            }
            else
            {
                row.Favorite = evt.Favorite;
            }

            await context.SaveChangesAsync();
        }
    }
}
