using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemEquippedHandler(GameContext context) : IPlayerUpdateHandler<ItemEquippedEvent>
    {
        // Bounded re-attempts on a (player, slot) unique violation, mirroring UserLogins.SaveWithConflictRetry.
        // Unlike ModAppliedHandler — whose conflict is the row it already owns (its key includes the slot),
        // settled by a single ExecuteUpdate that can't re-violate — an equip must *evict a different item* from
        // the destination slot, so the vacate and the place are two writes with an unavoidable window. A
        // concurrent apply of the same at-least-once event, an ItemUnlockedEvent reordered behind this one, or a
        // cross-instance writer can re-occupy the slot between the vacate and the save and re-trip the index, so
        // the retry here genuinely can re-violate (ModAppliedHandler's cannot). Each pass re-vacates whoever won
        // that race and re-places this item, so transient contention converges in-handler instead of burning the
        // queue's coarser per-event attempt budget; a still-failing final attempt propagates to that queue
        // retry/dead-letter backstop (at-least-once + idempotent handlers, so nothing is lost) rather than looping.
        private const int MaxSaveAttempts = 3;

        public async Task HandleAsync(ItemEquippedEvent evt)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await ApplyAsync(evt);
                    return;
                }
                catch (DbUpdateException ex) when (attempt < MaxSaveAttempts && ex.IsUniqueViolation())
                {
                    context.ChangeTracker.Clear();
                }
            }
        }

        private async Task ApplyAsync(ItemEquippedEvent evt)
        {
            // Vacate the destination slot first with a single server-side statement — no prior occupant is
            // materialized into a snapshot a concurrent commit could tear, so the upsert below can't collide with
            // it on the (player, slot) unique index. Mirrors ModAppliedHandler's clear-then-write. The clear and
            // the upsert are separate commits, so a crash between them leaves the slot momentarily empty; the
            // queue's reserve/acknowledge read redelivers the event and converges, so there is no lost write.
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
