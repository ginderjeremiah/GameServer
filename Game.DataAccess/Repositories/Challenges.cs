using Game.Abstractions.DataAccess;
using Game.Core.Progress;
using Game.DataAccess.Repositories.Caching;

namespace Game.DataAccess.Repositories
{
    internal class Challenges(ChallengesCacheHolder holder) : IChallenges
    {
        public IReadOnlyList<Challenge> All()
        {
            // holder.Current is already an immutable snapshot, so return it directly rather than copying it
            // on every battle's challenge evaluation (the hottest caller). Callers treat it as read-only.
            return holder.Current;
        }

        public bool ValidateChallengeId(int challengeId)
        {
            return challengeId >= 0 && challengeId < holder.Current.Count;
        }

        public Challenge GetChallenge(int challengeId)
        {
            return holder.Current.GetById(challengeId, "challenge");
        }
    }
}
