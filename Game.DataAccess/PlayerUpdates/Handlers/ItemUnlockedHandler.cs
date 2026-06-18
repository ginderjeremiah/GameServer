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
            // The existence check skips the common re-apply without touching the row (no tracking needed on
            // this hot-path read), but isn't atomic with the insert. A concurrent apply of the same
            // at-least-once event can insert the row between the check and the save, so the unique-violation
            // catch absorbs that race as a benign no-op — the (player, item) primary key already holds it.
            var exists = await context.UnlockedItems
                .AsNoTracking()
                .AnyAsync(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId);

            if (exists)
            {
                return;
            }

            context.UnlockedItems.Add(new UnlockedItem
            {
                PlayerId = evt.PlayerId,
                ItemId = evt.ItemId,
            });

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply inserted the same (player, item) first; the row exists, so this is a no-op.
            }
        }
    }
}
