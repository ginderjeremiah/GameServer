using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModAppliedHandler(GameContext context) : IPlayerUpdateHandler<ModAppliedEvent>
    {
        public async Task HandleAsync(ModAppliedEvent evt)
        {
            // Remove existing mod in the same slot
            await context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();

            context.AppliedMods.Add(new AppliedMod
            {
                PlayerId = evt.PlayerId,
                ItemId = evt.ItemId,
                ItemModSlotId = evt.ItemModSlotId,
                ItemModId = evt.ItemModId,
            });
            await context.SaveChangesAsync();
        }
    }
}
