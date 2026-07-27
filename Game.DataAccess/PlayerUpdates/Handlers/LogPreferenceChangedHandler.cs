using System.Globalization;
using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class LogPreferenceChangedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<LogPreferenceChangedEvent>
    {
        // Keyed per log type: each event carries one type's flag, so a per-player key would discard an older
        // change to type A merely because a newer change to type B landed first. Formatted invariantly because
        // the key is a persisted comparison key — a culture that renders digits differently would write a
        // second watermark row and the guard would silently stop seeing the first.
        public Task HandleAsync(LogPreferenceChangedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.LogPreference,
                [((int)evt.LogType).ToString(CultureInfo.InvariantCulture)],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, LogPreferenceChangedEvent evt)
        {
            var logTypeId = (int)evt.LogType;

            // Absolute upsert: attempt the update first; if no row exists yet (rows-affected 0) fall through to
            // the insert. Both run on the guard's context, so they are inside its transaction rather than each
            // committing on their own — the watermark advance and this write have to land or roll back together.
            // The update and insert still aren't atomic against a concurrent apply, which can insert the row in
            // between; the guard's restart re-runs the whole attempt and the update then finds the row.
            Task<int> SetEnabledAsync() => context.LogPreferences
                .Where(lp => lp.PlayerId == evt.PlayerId && lp.LogTypeId == logTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(lp => lp.Enabled, evt.Enabled));

            if (await SetEnabledAsync() > 0)
            {
                return;
            }

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
