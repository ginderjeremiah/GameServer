using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ItemUnlockedHandler(GameContext context) : IPlayerUpdateHandler<ItemUnlockedEvent>
    {
        public Task HandleAsync(ItemUnlockedEvent evt)
        {
            return context.InsertIfMissingAsync(
                (UnlockedItem ui) => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId,
                () => new UnlockedItem
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                });
        }
    }
}
