using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModRemovedHandler(GameContext context) : IPlayerUpdateHandler<ModRemovedEvent>
    {
        public async Task HandleAsync(ModRemovedEvent evt)
        {
            await context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();
        }
    }
}
