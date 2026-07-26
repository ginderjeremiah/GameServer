using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class PlayerCoreUpdatedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<PlayerCoreUpdatedEvent>
    {
        // Absolute writes over the whole player row, so a stale replay would durably regress level/exp/zone
        // until the player dirties them again. One target: the row itself is the finest granularity there is.
        // The context comes from the guard rather than this constructor so the write can only ever be issued
        // on the connection whose transaction covers the watermark advance.
        public Task HandleAsync(PlayerCoreUpdatedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.PlayerCore,
                [PlayerWriteWatermarkGuard.PlayerScopedTarget],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, PlayerCoreUpdatedEvent evt)
        {
            await context.Players
                .Where(p => p.Id == evt.PlayerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Level, evt.Level)
                    .SetProperty(p => p.Exp, evt.Exp)
                    .SetProperty(p => p.CurrentZoneId, evt.CurrentZoneId)
                    .SetProperty(p => p.StatPointsGained, evt.StatPointsGained)
                    .SetProperty(p => p.StatPointsUsed, evt.StatPointsUsed)
                    .SetProperty(p => p.LastActivity, evt.LastActivity)
                    .SetProperty(p => p.AutoChallengeBoss, evt.AutoChallengeBoss)
                    .SetProperty(p => p.LastCreditedBattleSeed, evt.LastCreditedBattleSeed));
        }
    }
}
