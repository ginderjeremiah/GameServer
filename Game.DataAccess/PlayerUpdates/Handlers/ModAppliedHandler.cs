using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModAppliedHandler(GameContext context) : IPlayerUpdateHandler<ModAppliedEvent>
    {
        public async Task HandleAsync(ModAppliedEvent evt)
        {
            // Remove existing mod in the same slot
            await context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();

            context.AppliedMods.Add(new AppliedMod
            {
                PlayerId = evt.PlayerId,
                ItemId = evt.ItemId,
                ItemModSlotId = evt.ItemModSlotId,
                ItemModId = evt.ItemModId,
            });

            // The delete-then-insert isn't atomic, so a concurrent apply of the same at-least-once event can
            // both no-op the delete and both insert the same (player, item, slot) row — the unique-violation
            // catch absorbs that race as a benign no-op, since both applies carry the identical ItemModId.
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply inserted the same (player, item, slot) first; the row exists, so this is a no-op.
            }
        }
    }
}
