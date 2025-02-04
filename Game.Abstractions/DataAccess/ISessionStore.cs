using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    public interface ISessionStore
    {
        public Task<PlayerState?> GetSession(string sessionId);
        public void Update(PlayerState sessionData, int playerId);
        public void SetBattleDataHash(int playerId, string activeEnemyHash);
        public Task<string?> GetAndDeleteBattleDataHash(int playerId);
    }
}
