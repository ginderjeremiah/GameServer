using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class SkillUnlockedHandler(GameContext context) : IPlayerUpdateHandler<SkillUnlockedEvent>
    {
        public Task HandleAsync(SkillUnlockedEvent evt)
        {
            // Earning a skill unlocks it without equipping it: Selected = false, Order = 0
            // (the player chooses their loadout separately).
            return context.InsertIfMissingAsync(
                (PlayerSkill ps) => ps.PlayerId == evt.PlayerId && ps.SkillId == evt.SkillId,
                () => new PlayerSkill
                {
                    PlayerId = evt.PlayerId,
                    SkillId = evt.SkillId,
                    Selected = false,
                    Order = 0,
                });
        }
    }
}
