using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    public interface ISessionStore
    {
        public Task<PlayerState?> GetSession(string sessionId);
        public void Update(PlayerState sessionData, int playerId);
    }
}
