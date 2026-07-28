using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ModRemovedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ModRemovedEvent>
    {
        // Guarded on the same mod slot key ModAppliedHandler uses, so the two order against each other. The
        // delete leaves no data row to carry a version, and the watermark row it advances is what rejects a
        // stale apply replayed afterwards instead of letting it resurrect the removed mod.
        public Task HandleAsync(ModRemovedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.Mods,
                [PlayerWriteTargets.Mods.Slot(evt.ItemId, evt.ItemModSlotId)],
                (context, _) => ApplyAsync(context, evt));

        private static Task ApplyAsync(GameContext context, ModRemovedEvent evt)
            => context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();
    }
}
