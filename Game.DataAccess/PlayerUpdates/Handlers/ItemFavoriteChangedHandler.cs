using System.Globalization;
using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemFavoriteChangedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ItemFavoriteChangedEvent>
    {
        // Keyed per item: each event carries one item's flag, so a per-player key would discard an older
        // change to item A merely because a newer change to item B landed first. Formatted invariantly
        // because the key is a persisted comparison key — a culture that renders digits differently would
        // write a second watermark row and the guard would silently stop seeing the first.
        public Task HandleAsync(ItemFavoriteChangedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.ItemFavorite,
                [evt.ItemId.ToString(CultureInfo.InvariantCulture)],
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
