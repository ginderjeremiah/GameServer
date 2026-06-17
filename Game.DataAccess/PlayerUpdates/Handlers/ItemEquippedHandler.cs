using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemEquippedHandler(GameContext context) : IPlayerUpdateHandler<ItemEquippedEvent>
    {
        public async Task HandleAsync(ItemEquippedEvent evt)
        {
            // Absolute per-slot upsert in a single atomic UPDATE: the equipped item takes the slot and any
            // prior occupant is cleared, in one statement. The previous two-statement clear-then-set left a
            // window where a crash between the writes (the queue read is a destructive pop, so the event is
            // not redelivered) emptied the slot for good. Folding both into one CASE-driven update over the
            // affected rows removes that window and stays idempotent — re-applying converges to the same state,
            // including when the item is moving from another slot (its own row is reassigned, vacating the old).
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && (ui.ItemId == evt.ItemId || ui.EquipmentSlotId == evt.SlotId))
                .ExecuteUpdateAsync(s => s.SetProperty(
                    ui => ui.EquipmentSlotId,
                    ui => ui.ItemId == evt.ItemId ? evt.SlotId : (int?)null));
        }
    }
}
