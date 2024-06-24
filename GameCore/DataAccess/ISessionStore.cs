using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ISessionStore
    {
        public Task<SessionData?> GetSessionAsync(string id);
        public Task<SessionData> GetNewSessionDataAsync(int playerId);
        public Task UpdateAsync(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
