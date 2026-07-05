using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class LessonUnlockedHandler(GameContext context) : IPlayerUpdateHandler<LessonUnlockedEvent>
    {
        public async Task HandleAsync(LessonUnlockedEvent evt)
        {
            // Unlocking an already-unlocked lesson is a domain no-op (Player.UnlockLesson), so this event only
            // ever fires once per lesson — a plain insert, with the unique-violation catch absorbing a
            // replayed/duplicate delivery of the same event.
            context.PlayerLessons.Add(new PlayerLesson
            {
                PlayerId = evt.PlayerId,
                LessonId = evt.LessonId,
                UnlockedAt = evt.UnlockedAt,
            });

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
            }
        }
    }
}
