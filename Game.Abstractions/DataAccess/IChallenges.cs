using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IChallenges
    {
        public IReadOnlyList<Challenge> All();
        // Whether a challenge with the given id exists; an O(1) range check (challenges are zero-based-id reference data).
        public bool ValidateChallengeId(int challengeId);
        public Challenge GetChallenge(int challengeId);
        // The precomputed statistic -> challenges reverse index, bundled with (and swapped atomically
        // alongside) the cached list, so per-battle challenge evaluation is scoped without re-indexing.
        public ChallengeIndex Index();

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
