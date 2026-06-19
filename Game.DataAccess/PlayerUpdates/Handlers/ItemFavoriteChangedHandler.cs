using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemFavoriteChangedHandler(GameContext context) : IPlayerUpdateHandler<ItemFavoriteChangedEvent>
    {
        public async Task HandleAsync(ItemFavoriteChangedEvent evt)
        {
            // The load-then-upsert isn't atomic, so a concurrent apply of the same at-least-once event — or an
            // ItemUnlockedEvent reordered behind this one — can insert the (player, item) row between our load
            // and save. On the resulting unique violation, clear and re-run once: the now-existing row loads as
            // an update, so the second pass carries no conflicting insert. A second failure propagates to the
            // queue's retry policy rather than looping. (Mirrors AttributeAllocationsChangedHandler.)
            try
            {
                await ApplyAsync(evt);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
                await ApplyAsync(evt);
            }
        }

        private async Task ApplyAsync(ItemFavoriteChangedEvent evt)
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
