using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    public interface ISessionStore
    {
        public Task<PlayerState?> GetSession(int userId);
        public void Update(PlayerState sessionData, int playerId);
        public void Clear(int userId);
    }
}
