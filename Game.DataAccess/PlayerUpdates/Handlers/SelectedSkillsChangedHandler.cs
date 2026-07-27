using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class SelectedSkillsChangedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<SelectedSkillsChangedEvent>
    {
        // The apply rebuilds every one of the player's Selected/Order columns from the event's full ordered
        // loadout, so a stale replay durably restores a loadout the player has already replaced. The target is
        // the loadout as a unit — a per-skill key would let the rows of one event partially win, leaving a
        // loadout that was never actually selected (a skill deselected by the newer event but re-Selected by
        // the older one's rebuild). The guard owns the transaction, the context, and the restart.
        public Task HandleAsync(SelectedSkillsChangedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.SelectedSkills,
                [PlayerWriteWatermarkGuard.PlayerScopedTarget],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, SelectedSkillsChangedEvent evt)
        {
            // Rebuild Selected/Order from the event's full ordered loadout, applied as a single write: fetch the
            // player's skill rows, reset every flag, then mark each id in the loadout Selected = true with its
            // index as Order. A loadout id with no existing row — its SkillUnlockedEvent reordered behind this
            // event under best-effort cross-instance ordering — is inserted rather than silently dropped, so the
            // just-equipped skill survives instead of waiting for a later loadout change to self-heal the DB.
            var playerSkills = await context.PlayerSkills
                .Where(ps => ps.PlayerId == evt.PlayerId)
                .ToListAsync();

            var orderBySkillId = new Dictionary<int, int>(evt.OrderedSkillIds.Count);
            for (var index = 0; index < evt.OrderedSkillIds.Count; index++)
            {
                orderBySkillId[evt.OrderedSkillIds[index]] = index;
            }

            var existingSkillIds = new HashSet<int>(playerSkills.Count);
            foreach (var playerSkill in playerSkills)
            {
                existingSkillIds.Add(playerSkill.SkillId);
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

            foreach (var (skillId, order) in orderBySkillId)
            {
                if (!existingSkillIds.Contains(skillId))
                {
                    context.PlayerSkills.Add(new PlayerSkill
                    {
                        PlayerId = evt.PlayerId,
                        SkillId = skillId,
                        Selected = true,
                        Order = order,
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
