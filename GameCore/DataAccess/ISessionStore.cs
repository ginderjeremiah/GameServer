using GameCore.Entities.SessionStore;
using System.Diagnostics.CodeAnalysis;

namespace GameCore.DataAccess
{
    public interface ISessionStore
    {
        public bool TryGetSession(string id, [NotNullWhen(true)] out SessionData? session);
        public SessionData GetNewSessionData(int playerId);
        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
