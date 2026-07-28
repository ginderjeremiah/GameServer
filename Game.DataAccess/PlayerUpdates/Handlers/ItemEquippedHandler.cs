using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemEquippedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ItemEquippedEvent>
    {
        // Guarded on both the item and the destination slot, all-or-nothing: an equip is one indivisible
        // change to two identities, and either key alone leaves a hole (see PlayerWriteStream.Equipment).
        // Stamping the evicted occupant instead isn't an option — the vacate below is a single ExecuteUpdate
        // precisely so no prior occupant is materialized, and ExecuteUpdate can't report the id it cleared.
        //
        // The guard also owns the transaction and the unique-violation restart the vacate-then-place needs.
        // That replaces this handler's own bounded retry loop: same-player sequenced equips now queue behind
        // each other on the slot's watermark row, so the cross-instance re-occupation that loop existed to
        // absorb can no longer interleave here. What still can — an unsequenced envelope bypassing the guard,
        // or a reordered ItemUnlockedEvent inserting the item's row — is a unique violation, and the guard's
        // restart re-runs the whole apply (vacate included) against it. A still-failing restart propagates to
        // the queue's retry/dead-letter backstop, exactly as the exhausted in-handler bound used to.
        public Task HandleAsync(ItemEquippedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.Equipment,
                [PlayerWriteTargets.Equipment.Item(evt.ItemId), PlayerWriteTargets.Equipment.Slot(evt.SlotId)],
                (context, _) => ApplyAsync(context, evt),
                allTargetsRequired: true);

        private static async Task ApplyAsync(GameContext context, ItemEquippedEvent evt)
        {
            // Vacate the destination slot first with a single server-side statement — no prior occupant is
            // materialized into a snapshot a concurrent commit could tear, so the upsert below can't collide with
            // it on the (player, slot) unique index. Both writes are inside the guard's transaction, so the slot
            // is never observably empty between them and a crash rolls back to the pre-equip state rather than
            // leaving it vacated.
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.EquipmentSlotId == evt.SlotId && ui.ItemId != evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));

            // Place the equipped item, inserting its row when its ItemUnlockedEvent reordered behind this equip
            // (rather than an ExecuteUpdate's silent zero-row no-op leaving the slot empty until the next equip).
            // Idempotent: re-applying converges, including when the item moves from another slot — its own row is
            // reassigned to the new slot, vacating the old.
            var target = await context.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId);

            if (target is null)
            {
                context.UnlockedItems.Add(new UnlockedItem
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                    EquipmentSlotId = evt.SlotId,
                });
            }
            else
            {
                target.EquipmentSlotId = evt.SlotId;
            }

            await context.SaveChangesAsync();
        }
    }
}
