using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IChallenges
    {
        public List<Challenge> All();
        public Challenge? GetChallenge(int challengeId);
    }
}
