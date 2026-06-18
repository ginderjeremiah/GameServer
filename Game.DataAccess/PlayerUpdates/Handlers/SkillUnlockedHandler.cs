using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class SkillUnlockedHandler(GameContext context) : IPlayerUpdateHandler<SkillUnlockedEvent>
    {
        public async Task HandleAsync(SkillUnlockedEvent evt)
        {
            // The existence check skips the common re-apply without touching the row (no tracking needed on
            // this hot-path read), but isn't atomic with the insert. A concurrent apply of the same
            // at-least-once event can insert the row between the check and the save, so the unique-violation
            // catch absorbs that race as a benign no-op — the (player, skill) primary key already holds it.
            var exists = await context.PlayerSkills
                .AsNoTracking()
                .AnyAsync(ps => ps.PlayerId == evt.PlayerId && ps.SkillId == evt.SkillId);

            if (exists)
            {
                return;
            }

            // Earning a skill unlocks it without equipping it: Selected = false, Order = 0
            // (the player chooses their loadout separately).
            context.PlayerSkills.Add(new PlayerSkill
            {
                PlayerId = evt.PlayerId,
                SkillId = evt.SkillId,
                Selected = false,
                Order = 0,
            });

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply inserted the same (player, skill) first; the row exists, so this is a no-op.
            }
        }
    }
}
