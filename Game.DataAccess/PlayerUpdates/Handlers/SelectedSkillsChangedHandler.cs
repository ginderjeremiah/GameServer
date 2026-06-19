using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class SelectedSkillsChangedHandler(GameContext context) : IPlayerUpdateHandler<SelectedSkillsChangedEvent>
    {
        public async Task HandleAsync(SelectedSkillsChangedEvent evt)
        {
            // The load-then-upsert isn't atomic, so a concurrent apply of the same at-least-once event — or a
            // SkillUnlockedEvent reordered behind this one — can insert a (player, skill) row between our load
            // and save. On the resulting unique violation, clear and re-run once: the now-existing row loads as
            // an update, so the second pass carries no conflicting insert. A second failure propagates to the
            // queue's retry policy rather than looping. (Mirrors AttributeAllocationsChangedHandler.)
            try
            {
                await ApplyAsync(evt);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
                await ApplyAsync(evt);
            }
        }

        private async Task ApplyAsync(SelectedSkillsChangedEvent evt)
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
