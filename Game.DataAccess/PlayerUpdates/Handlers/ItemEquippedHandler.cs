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

        private async Task ApplyAsync(ItemEquippedEvent evt)
        {
            // Absolute per-slot upsert in a single transaction: the equipped item takes the slot and any prior
            // occupant is cleared. Loading the affected rows lets us insert the equipped item's row when it is
            // missing — its ItemUnlockedEvent reordered behind this equip under best-effort cross-instance
            // ordering — rather than ExecuteUpdate's silent zero-row no-op leaving the slot empty until the next
            // equip. Idempotent: re-applying converges to the same state, including when the item moves from
            // another slot (its own row is reassigned, vacating the old). A single SaveChanges keeps it atomic,
            // and the queue's reserve/acknowledge read redelivers on a crash, so there is no lost-write window.
            var affected = await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && (ui.ItemId == evt.ItemId || ui.EquipmentSlotId == evt.SlotId))
                .ToListAsync();

            UnlockedItem? target = null;
            foreach (var row in affected)
            {
                if (row.ItemId == evt.ItemId)
                {
                    target = row;
                }
                else if (row.EquipmentSlotId == evt.SlotId)
                {
                    // Clear whatever currently occupies the destination slot.
                    row.EquipmentSlotId = null;
                }
            }

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
