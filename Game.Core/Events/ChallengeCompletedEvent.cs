namespace Game.Core.Events
{
    /// <summary>
    /// Raised when a player completes a challenge.
    /// </summary>
    public record ChallengeCompletedEvent(
        int PlayerId,
        int ChallengeId,
        int? RewardItemId,
        int? RewardItemModId) : IDomainEvent;
}
