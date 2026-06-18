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

            // Absolute upsert: attempt the update first as a single self-committing write; if no row exists yet
            // (rows-affected 0) fall through to the insert. The update and insert aren't atomic together, so a
            // concurrent apply can insert the row in between — on the unique violation, re-run the absolute
            // update so this event's value still lands on the now-existing row. Re-applying always converges.
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

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Lost the insert race; clear the failed insert and set the now-existing row's value absolutely.
                context.ChangeTracker.Clear();
                await SetEnabledAsync();
            }
        }
    }
}
