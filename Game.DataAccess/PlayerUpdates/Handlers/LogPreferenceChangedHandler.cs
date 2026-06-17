using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class LogPreferenceChangedHandler(GameContext context) : IPlayerUpdateHandler<LogPreferenceChangedEvent>
    {
        public async Task HandleAsync(LogPreferenceChangedEvent evt)
        {
            var logTypeId = (int)evt.LogType;

            // Idempotent upsert mirroring HandleItemFavoriteChanged: attempt the absolute update first as a
            // single self-committing write; if no row exists yet (rows-affected 0) fall through to the insert.
            // Re-applying the event converges to the same state under the write-behind retry policy.
            var updated = await context.LogPreferences
                .Where(lp => lp.PlayerId == evt.PlayerId && lp.LogTypeId == logTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(lp => lp.Enabled, evt.Enabled));

            if (updated == 0)
            {
                context.LogPreferences.Add(new LogPreference
                {
                    PlayerId = evt.PlayerId,
                    LogTypeId = logTypeId,
                    Enabled = evt.Enabled,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
