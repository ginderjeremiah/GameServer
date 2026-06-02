using Game.Core.Progress;

namespace Game.Abstractions.DataAccess
{
    public interface IChallenges
    {
        public List<Challenge> All();
        public Challenge GetChallenge(int challengeId);
    }
}
