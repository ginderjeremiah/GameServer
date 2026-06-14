using Game.Core.Players;

namespace Game.Abstractions.DataAccess
{
    // Sessions are keyed by the user/account id (not PlayerState.PlayerId, which is a distinct value).
    public interface ISessionStore
    {
        public Task<PlayerState?> GetSession(int userId);
        public void Update(PlayerState sessionData, int userId);
        public void Clear(int userId);
    }
}
