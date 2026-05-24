using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface IPlayerChallenges
    {
        public Task<List<PlayerChallenge>> GetPlayerChallenges(int playerId);
        public Task UpdateProgress(int playerId, int challengeId, int progress);
        public Task CompleteChallenge(int playerId, int challengeId);
    }
}
