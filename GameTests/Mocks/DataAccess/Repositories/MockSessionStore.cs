using GameCore.DataAccess;
using GameCore.Entities.SessionStore;
using GameCore.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockSessionStore : ISessionStore
    {
        public ICacheService Cache { get; set; }
        public MockSessionStore(ICacheService cache)
        {
            Cache = cache;
        }

        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData)
        {
            throw new NotImplementedException();
        }

        public SessionData GetNewSessionData(int playerId)
        {
            throw new NotImplementedException();
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            throw new NotImplementedException();
        }

        public bool TryGetSession(string id, [NotNullWhen(true)] out SessionData? session)
        {
            return Cache.TryGet(id, out session);
        }

        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty)
        {
            throw new NotImplementedException();
        }
    }
}
