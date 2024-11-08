﻿using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface ISessionStore
    {
        public Task<SessionData?> GetSessionAsync(int playerId);
        public Task<SessionData> GetNewSessionDataAsync(int playerId);
        public void Update(SessionData sessionData);
        public void SetActiveEnemyHash(SessionData sessionData, string activeEnemyHash);
        public string? GetAndDeleteActiveEnemyHash(SessionData sessionData);
    }
}
