using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerChallenges
    {
        public Task<List<PlayerChallenge>> GetPlayerChallenges(int playerId);
    }
}
