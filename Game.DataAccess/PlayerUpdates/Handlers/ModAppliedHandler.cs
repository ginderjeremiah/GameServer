using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModAppliedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ModAppliedEvent>
    {
        // Shares the mods stream — and therefore the mod slot's watermark row — with ModRemovedHandler, which
        // is the point of guarding on a separate row rather than versioning the data row: a remove deletes the
        // AppliedMod outright, and a stale apply arriving afterwards would find nothing to compare against and
        // resurrect the mod. The watermark outlives the row it guards.
        public Task HandleAsync(ModAppliedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.Mods,
                [PlayerWriteTargets.Mods.Slot(evt.ItemId, evt.ItemModSlotId)],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, ModAppliedEvent evt)
        {
            // Load-then-upsert, the shape ProgressUpdatedHandler uses. The prior delete-then-insert existed
            // only because the AppliedMod primary key excludes ItemModId, so a colliding apply can carry a
            // *different* mod and absorbing the violation as a no-op would drop this one — but writing
            // ItemModId onto the row that is already there settles that directly, without the delete.
            //
            // Dropping it also lets the guard own the unique-violation restart outright. A writer the
            // watermark doesn't serialize (an unsequenced envelope, which bypasses the guard) can still insert
            // the row between this load and the save; on the restart the row exists and this becomes an update,
            // which cannot re-violate. The old bespoke catch can't be kept alongside the guard — it settled the
            // conflict with a second write, and a DbUpdateException aborts the surrounding transaction, so that
            // write would land in a transaction that can no longer commit.
            var applied = await context.AppliedMods
                .FirstOrDefaultAsync(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId);

            if (applied is null)
            {
                context.AppliedMods.Add(new AppliedMod
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                    ItemModSlotId = evt.ItemModSlotId,
                    ItemModId = evt.ItemModId,
                });
            }
            else
            {
                applied.ItemModId = evt.ItemModId;
            }

            await context.SaveChangesAsync();
        }
    }
}
