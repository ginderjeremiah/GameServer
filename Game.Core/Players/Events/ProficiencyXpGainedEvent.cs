using Game.Core.Events;
using Game.Core.Proficiencies;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a won battle accrues proficiency XP (live path only — the offline batch suppresses it in
    /// favour of the welcome-back summary). Carries every proficiency the battle trained, so the API layer
    /// can push one update to the connected client and a level-up or milestone becomes visible immediately
    /// rather than only after a refresh — mirroring <see cref="ChallengeCompletedEvent"/>.
    /// </summary>
    public record ProficiencyXpGainedEvent(
        int PlayerId,
        IReadOnlyList<ProficiencyXpResult> Results) : IDomainEvent, ILoggableDomainEvent
    {
        // Curated safe scalars only — never the per-proficiency payload itself.
        public IReadOnlyList<KeyValuePair<string, object?>> GetLogProperties() =>
        [
            new("PlayerId", PlayerId),
            new("ProficienciesTrained", Results.Count),
        ];
    }
}
