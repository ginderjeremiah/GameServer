using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ISessionStore
    {
        public Task<SessionData?> GetSessionAsync(string id);
        public Task<SessionData> GetNewSessionDataAsync(int playerId);
        public void Update(SessionData sessionData);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
