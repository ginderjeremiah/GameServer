using Game.Abstractions.DataAccess;
using Game.Core.Progress;
using Game.DataAccess.Repositories.Caching;

namespace Game.DataAccess.Repositories
{
    internal class Challenges(ChallengesCacheHolder holder) : IChallenges
    {
        public List<Challenge> All()
        {
            return [.. holder.Current];
        }

        public Challenge GetChallenge(int challengeId)
        {
            return holder.Current[challengeId];
        }
    }
}
