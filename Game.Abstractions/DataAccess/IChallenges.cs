using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IChallenges
    {
        public IReadOnlyList<Challenge> All();
        public Challenge GetChallenge(int challengeId);
    }
}
