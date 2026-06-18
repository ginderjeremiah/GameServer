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
            // The existence check skips the common re-apply without touching the row (no tracking needed on
            // this hot-path read), but isn't atomic with the insert. A concurrent apply of the same
            // at-least-once event can insert the row between the check and the save, so the unique-violation
            // catch absorbs that race as a benign no-op — the (player, mod) primary key already holds it.
            var exists = await context.UnlockedMods
                .AsNoTracking()
                .AnyAsync(um => um.PlayerId == evt.PlayerId && um.ItemModId == evt.ItemModId);

            if (exists)
            {
                return;
            }

            context.UnlockedMods.Add(new UnlockedMod
            {
                PlayerId = evt.PlayerId,
                ItemModId = evt.ItemModId,
            });

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply inserted the same (player, mod) first; the row exists, so this is a no-op.
            }
        }
    }
}
