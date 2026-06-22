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
            // The delete-then-insert isn't atomic, so a concurrent or reordered apply can insert the
            // (player, item, slot) row between our delete and save. The AppliedMod PK excludes ItemModId, so a
            // colliding apply can carry a *different* mod — absorbing the violation as a no-op (as the prior code
            // did) would terminally drop ours. On the unique violation the slot row already exists, so settle it
            // with a server-side last-writer-wins update to our mod: an update can't re-violate the PK, so it
            // converges without looping or propagating, and an identical double-apply is a harmless self-write.
            try
            {
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

                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
                await context.AppliedMods
                    .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                    .ExecuteUpdateAsync(s => s.SetProperty(am => am.ItemModId, evt.ItemModId));
            }
        }
    }
}
