using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class SelectedSkillsChangedHandler(GameContext context) : IPlayerUpdateHandler<SelectedSkillsChangedEvent>
    {
        public async Task HandleAsync(SelectedSkillsChangedEvent evt)
        {
            // Delete-then-rebuild for idempotency, applied as a single write (the same shape as the
            // attribute-allocations handler): fetch the player's skill rows, reset every flag, then mark
            // each id in the ordered loadout Selected = true with its index as Order. Re-applying the event
            // converges to the same state, and EF batches the touched rows into one round-trip rather than
            // issuing one ExecuteUpdate per skill.
            var playerSkills = await context.PlayerSkills
                .Where(ps => ps.PlayerId == evt.PlayerId)
                .ToListAsync();

            var orderBySkillId = new Dictionary<int, int>(evt.OrderedSkillIds.Count);
            for (var index = 0; index < evt.OrderedSkillIds.Count; index++)
            {
                orderBySkillId[evt.OrderedSkillIds[index]] = index;
            }

            foreach (var playerSkill in playerSkills)
            {
                if (orderBySkillId.TryGetValue(playerSkill.SkillId, out var order))
                {
                    playerSkill.Selected = true;
                    playerSkill.Order = order;
                }
                else
                {
                    playerSkill.Selected = false;
                    playerSkill.Order = 0;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
