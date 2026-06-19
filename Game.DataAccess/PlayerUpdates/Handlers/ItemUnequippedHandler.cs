using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemUnequippedHandler(GameContext context) : IPlayerUpdateHandler<ItemUnequippedEvent>
    {
        public async Task HandleAsync(ItemUnequippedEvent evt)
        {
            // Idempotent absolute update: clear the item's slot. Unlike the equip/select handlers this needs no
            // insert-if-missing — "unequipped" is exactly the absence of an equipped row. If this event is
            // reordered ahead of the item's ItemUnlockedEvent, the missing-row update is a benign no-op and the
            // later unlock inserts the row with a null slot (unequipped), so the end state still converges.
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));
        }
    }
}
