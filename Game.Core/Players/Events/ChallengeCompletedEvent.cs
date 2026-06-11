using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player completes a challenge. A single event describes the completion and any
    /// content it unlocked (the reward ids are null when the challenge carries no reward of that kind),
    /// which the API layer pushes to the connected client so newly-unlocked rewards become usable
    /// immediately rather than only after a refresh.
    /// </summary>
    public record ChallengeCompletedEvent(
        int PlayerId,
        int ChallengeId,
        int? RewardItemId,
        int? RewardItemModId,
        int? RewardSkillId) : IDomainEvent;
}
