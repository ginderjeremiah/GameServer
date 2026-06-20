using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModUnlockedHandler(GameContext context) : IPlayerUpdateHandler<ModUnlockedEvent>
    {
        public Task HandleAsync(ModUnlockedEvent evt)
        {
            return context.InsertIfMissingAsync(
                (UnlockedMod um) => um.PlayerId == evt.PlayerId && um.ItemModId == evt.ItemModId,
                () => new UnlockedMod
                {
                    PlayerId = evt.PlayerId,
                    ItemModId = evt.ItemModId,
                });
        }
    }
}
