using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemUnequippedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ItemUnequippedEvent>
    {
        // Shares the equipment stream with ItemEquippedHandler, and only the item key: an unequip names no
        // slot, and it must order against the equips that do — a stale unequip replayed after a newer equip
        // would otherwise durably strip a worn item, which Redis then never corrects until the player touches
        // that slot again. Its own "missing row is already the desired end state" convergence covers the
        // reordered-behind-its-unlock case only; it says nothing about arriving after a *newer* equip.
        public Task HandleAsync(ItemUnequippedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.Equipment,
                [PlayerWriteTargets.Equipment.Item(evt.ItemId)],
                (context, _) => ApplyAsync(context, evt));

        // Idempotent absolute update: clear the item's slot. Unlike the equip/select handlers this needs no
        // insert-if-missing — "unequipped" is exactly the absence of an equipped row. If this event is
        // reordered ahead of the item's ItemUnlockedEvent, the missing-row update is a benign no-op and the
        // later unlock inserts the row with a null slot (unequipped), so the end state still converges.
        private static Task ApplyAsync(GameContext context, ItemUnequippedEvent evt)
            => context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));
    }
}
