using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface ISessionStore
    {
        public Task<SessionData?> GetSession(int playerId);
        public SessionData GetNewSessionData(int playerId);
        public void Update(SessionData sessionData);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
