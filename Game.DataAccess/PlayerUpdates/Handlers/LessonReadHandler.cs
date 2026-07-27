using System.Globalization;
using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class LessonReadHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<LessonReadEvent>
    {
        // Keyed per lesson, for the same reason as the log-preference stream: each event carries one lesson's
        // read state, so a per-player key would discard an older lesson's read merely because a newer one
        // landed first. Formatted invariantly because the key is a persisted comparison key.
        public Task HandleAsync(LessonReadEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.LessonRead,
                [evt.LessonId.ToString(CultureInfo.InvariantCulture)],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, LessonReadEvent evt)
        {
            // Absolute upsert (mirroring LogPreferenceChangedHandler): update first; if no row exists yet — a
            // screen-anchored lesson goes straight from locked to read with no prior LessonUnlockedEvent, and a
            // reordered delivery of that event behind this one is also possible — fall through to insert with
            // both timestamps the domain already resolved. Both writes run on the guard's context, inside its
            // transaction, and a row inserted concurrently in between is absorbed by the guard's restart.
            Task<int> SetReadAsync() => context.PlayerLessons
                .Where(pl => pl.PlayerId == evt.PlayerId && pl.LessonId == evt.LessonId)
                .ExecuteUpdateAsync(s => s.SetProperty(pl => pl.ReadAt, evt.ReadAt));

            if (await SetReadAsync() > 0)
            {
                return;
            }

            context.PlayerLessons.Add(new PlayerLesson
            {
                PlayerId = evt.PlayerId,
                LessonId = evt.LessonId,
                UnlockedAt = evt.UnlockedAt,
                ReadAt = evt.ReadAt,
            });

            await context.SaveChangesAsync();
        }
    }
}
