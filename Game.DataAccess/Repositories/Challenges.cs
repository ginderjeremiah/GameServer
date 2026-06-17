using Game.Abstractions.DataAccess;
using Game.Core.Progress;
using Game.DataAccess.Repositories.Caching;

namespace Game.DataAccess.Repositories
{
    internal class Challenges(ChallengesCacheHolder holder) : IChallenges
    {
        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => holder.Current;

        public IReadOnlyList<Challenge> All()
        {
            // The snapshot's list is already immutable, so return it directly rather than copying it on
            // every battle's challenge evaluation (the hottest caller). Callers treat it as read-only.
            return holder.Current.Challenges;
        }

        public bool ValidateChallengeId(int challengeId)
        {
            return challengeId >= 0 && challengeId < holder.Current.Challenges.Count;
        }

        public Challenge GetChallenge(int challengeId)
        {
            return holder.Current.Challenges.GetById(challengeId, "challenge");
        }

        public ChallengeIndex Index()
        {
            return holder.Current.Index;
        }
    }
}
