using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class PlayerCoreUpdatedHandler(GameContext context) : IPlayerUpdateHandler<PlayerCoreUpdatedEvent>
    {
        public async Task HandleAsync(PlayerCoreUpdatedEvent evt)
        {
            await context.Players
                .Where(p => p.Id == evt.PlayerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Level, evt.Level)
                    .SetProperty(p => p.Exp, evt.Exp)
                    .SetProperty(p => p.CurrentZoneId, evt.CurrentZoneId)
                    .SetProperty(p => p.StatPointsGained, evt.StatPointsGained)
                    .SetProperty(p => p.StatPointsUsed, evt.StatPointsUsed));
        }
    }
}
