using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemEquippedHandler(GameContext context) : IPlayerUpdateHandler<ItemEquippedEvent>
    {
        public async Task HandleAsync(ItemEquippedEvent evt)
        {
            // The clear-then-upsert isn't atomic, so a concurrent apply of the same at-least-once event — or an
            // ItemUnlockedEvent reordered behind this one — can take the destination slot between our clear and
            // save. On the resulting unique violation, re-run once: the re-run's server-side clear vacates the
            // row that won the race, so the second pass converges. A second failure propagates to the queue's
            // retry policy rather than looping. (Mirrors ModAppliedHandler.)
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
