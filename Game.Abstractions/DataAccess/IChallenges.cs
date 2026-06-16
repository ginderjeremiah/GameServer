using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IChallenges
    {
        public IReadOnlyList<Challenge> All();
        // Whether a challenge with the given id exists; an O(1) range check (challenges are zero-based-id reference data).
        public bool ValidateChallengeId(int challengeId);
        public Challenge GetChallenge(int challengeId);
    }
}
