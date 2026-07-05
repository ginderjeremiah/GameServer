using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class LessonReadHandler(GameContext context) : IPlayerUpdateHandler<LessonReadEvent>
    {
        public async Task HandleAsync(LessonReadEvent evt)
        {
            // Absolute upsert (mirroring LogPreferenceChangedHandler): update first; if no row exists yet — a
            // screen-anchored lesson goes straight from locked to read with no prior LessonUnlockedEvent, and a
            // reordered delivery of that event behind this one is also possible — fall through to insert with
            // both timestamps the domain already resolved.
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

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Lost the insert race to a concurrently-applied LessonUnlockedEvent; clear the failed insert
                // and set the now-existing row's ReadAt absolutely.
                context.ChangeTracker.Clear();
                await SetReadAsync();
            }
        }
    }
}
