using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// The canonical <see cref="ISessionStore"/> test double: an in-memory store that serves a single
    /// optional cached session and records reads and writes, so the cache-hit/miss, rehydration, and
    /// forced-reload paths can be exercised without Redis. With no <see cref="Session"/> assigned it is
    /// an inert no-op store, which is all most callers need — use it rather than adding another double.
    /// </summary>
    public sealed class FakeSessionStore : ISessionStore
    {
        public PlayerState? Session { get; set; }
        public List<(PlayerState State, int UserId)> Updates { get; } = [];
        public List<(PlayerState State, int UserId)> AsyncUpdates { get; } = [];
        public List<int> Cleared { get; } = [];
        public int GetSessionCalls { get; private set; }
        public CancellationToken LastGetSessionToken { get; private set; }
        public CancellationToken LastUpdateAsyncToken { get; private set; }

        public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default)
        {
            GetSessionCalls++;
            LastGetSessionToken = cancellationToken;
            return Task.FromResult(Session);
        }

        public void Update(PlayerState sessionData, int userId) => Updates.Add((sessionData, userId));

        public Task UpdateAsync(PlayerState sessionData, int userId, CancellationToken cancellationToken = default)
        {
            LastUpdateAsyncToken = cancellationToken;
            AsyncUpdates.Add((sessionData, userId));
            return Task.CompletedTask;
        }

        public void Clear(int userId) => Cleared.Add(userId);
    }
}
