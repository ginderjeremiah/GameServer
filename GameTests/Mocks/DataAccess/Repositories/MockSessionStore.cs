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
            return Cache.GetDelete($"ActiveEnemy_{sessionData.SessionId}");
        }

        public SessionData GetNewSessionData(int playerId)
        {
            return new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                PlayerData = new()
                {
                    PlayerId = playerId,
                    Salt = Guid.NewGuid()
                },
                InventoryItems = new(),
                Attributes = new(),
                PlayerSkills = new(),
            };
        }

        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash)
        {
            Cache.Set($"ActiveEnemy_{sessionData.SessionId}", activeEnemyHash);
        }

        public bool TryGetSession(string id, [NotNullWhen(true)] out SessionData? session)
        {
            return Cache.TryGet(id, out session);
        }

        public void Update(SessionData sessionData, bool playerDirty, bool skillsDirty, bool inventoryDirty)
        {
            Cache.Set(sessionData.SessionId, sessionData);
        }
    }
}
