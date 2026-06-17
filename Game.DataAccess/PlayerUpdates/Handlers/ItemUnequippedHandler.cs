using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemUnequippedHandler(GameContext context) : IPlayerUpdateHandler<ItemUnequippedEvent>
    {
        public async Task HandleAsync(ItemUnequippedEvent evt)
        {
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));
        }
    }
}
