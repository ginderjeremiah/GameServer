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
            var exists = await context.PlayerSkills
                .AnyAsync(ps => ps.PlayerId == evt.PlayerId && ps.SkillId == evt.SkillId);

            if (!exists)
            {
                // Earning a skill unlocks it without equipping it: Selected = false, Order = 0
                // (the player chooses their loadout separately). Idempotent insert mirrors the
                // item/mod unlock handlers so re-applying the event never duplicates the row.
                context.PlayerSkills.Add(new PlayerSkill
                {
                    PlayerId = evt.PlayerId,
                    SkillId = evt.SkillId,
                    Selected = false,
                    Order = 0,
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
