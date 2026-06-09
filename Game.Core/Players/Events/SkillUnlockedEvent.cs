using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player unlocks a new skill (e.g. as a challenge reward). The skill is added
    /// to the player's unlocked set but is not equipped — earning a skill does not auto-select it.
    /// </summary>
    public record SkillUnlockedEvent(
        int PlayerId,
        int SkillId) : IDomainEvent;
}
