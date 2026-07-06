using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Core.Players;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the request-scoped session identity contract: authentication is derived from the validated
    /// token (never the session-cache hit), an evicted/absent session is rehydrated rather than reported
    /// as not-logged-in, and the player aggregate is loaded once up front (by
    /// <c>SocketInterceptorMiddleware</c> on a socket connection) and then read synchronously via
    /// <see cref="SessionService.Player"/>, so commands never re-read the cache per command.
    /// </summary>
    public class SessionServiceTests
    {
        [Fact]
        public void SetAuthenticatedUser_AuthenticatesWithoutLoadingPlayerSession()
        {
            // Recording the user id is the cheap, per-request work: it authenticates the caller without
            // touching the session cache, so a request that never reads player state pays no cache read.
            var session = new SessionService(new FakeSessionStore());

            session.SetAuthenticatedUser(5);

            Assert.True(session.Authenticated);
            Assert.Equal(5, session.UserId);
            Assert.False(session.HasPlayerSession);
            Assert.Equal(0, session.SelectedPlayerId);
        }

        [Fact]
        public async Task LoadPlayerState_CacheHit_LoadsPlayerSession()
        {
            var store = new FakeSessionStore { Session = new PlayerState { PlayerId = 7 } };
            var session = new SessionService(store);
            session.SetAuthenticatedUser(5);

            await session.LoadPlayerState();

            Assert.True(session.HasPlayerSession);
            Assert.Equal(7, session.SelectedPlayerId);
        }

        [Fact]
        public async Task LoadPlayerState_CacheMiss_LeavesNoPlayerSession()
        {
            // An evicted/absent session leaves the user authenticated but with no player session yet, for the
            // caller to rehydrate; the token (recorded separately) remains the sole authority for login.
            var session = new SessionService(new FakeSessionStore());
            session.SetAuthenticatedUser(5);

            await session.LoadPlayerState();

            Assert.True(session.Authenticated);
            Assert.False(session.HasPlayerSession);
            Assert.Equal(0, session.SelectedPlayerId);
        }

        [Fact]
        public async Task LoadPlayerState_ForwardsCancellationTokenToStore()
        {
            // The token (HttpContext.RequestAborted) must reach the session store so a cancelled request
            // unwinds the cache read cooperatively rather than running it to completion.
            var store = new FakeSessionStore();
            var session = new SessionService(store);
            session.SetAuthenticatedUser(5);
            using var cts = new CancellationTokenSource();

            await session.LoadPlayerState(cts.Token);

            Assert.Equal(cts.Token, store.LastGetSessionToken);
        }

        [Fact]
        public void RehydrateSession_EstablishesSessionInMemoryWithoutWritingTheCache()
        {
            // Rehydration runs on the concurrent HTTP path, so it re-binds the player in memory only and must
            // not write the session cache — those writes belong on the socket's write-behind path (#937).
            var store = new FakeSessionStore();
            var session = new SessionService(store);
            session.SetAuthenticatedUser(5);

            session.RehydrateSession(7);

            Assert.True(session.HasPlayerSession);
            Assert.Equal(7, session.SelectedPlayerId);
            Assert.Empty(store.Updates);
        }

        [Fact]
        public void CreateSession_PrimesTheCache()
        {
            // Login establishes the binding before any socket exists, so it does prime the cache (the one HTTP
            // path that legitimately writes the session, at the start of the session lifecycle).
            var store = new FakeSessionStore();
            var session = new SessionService(store);

            session.CreateSession(userId: 5, playerId: 7);

            Assert.True(session.HasPlayerSession);
            Assert.Equal(7, session.SelectedPlayerId);
            var update = Assert.Single(store.Updates);
            Assert.Equal(5, update.UserId);
            Assert.Equal(7, update.State.PlayerId);
        }

        [Fact]
        public void Player_BeforeLoad_Throws()
        {
            var session = new SessionService(new FakeSessionStore());

            Assert.Throws<InvalidOperationException>(() => session.Player);
        }

        [Fact]
        public void Player_AfterSetPlayer_ReturnsSameInstance()
        {
            var session = new SessionService(new FakeSessionStore());
            var player = new PlayerBuilder().WithId(7).Build();

            session.SetPlayer(player);

            Assert.Same(player, session.Player);
        }

        [Fact]
        public void ClearSession_ResetsLoadedPlayer()
        {
            var session = new SessionService(new FakeSessionStore());
            session.SetPlayer(new PlayerBuilder().WithId(7).Build());

            session.ClearSession();

            Assert.Throws<InvalidOperationException>(() => session.Player);
        }

        [Fact]
        public void MarkPlayerNeedsReload_SetsPlayerNeedsReload()
        {
            var session = new SessionService(new FakeSessionStore());
            session.SetPlayer(new PlayerBuilder().WithId(7).Build());

            session.MarkPlayerNeedsReload();

            Assert.True(session.PlayerNeedsReload);
        }

        [Fact]
        public void SetPlayer_ClearsAPendingReloadMarker()
        {
            // A prior command's flush failure marks the session for reload (#1632); loading the fresh player
            // must clear that marker so the next command doesn't reload again unnecessarily.
            var session = new SessionService(new FakeSessionStore());
            session.SetPlayer(new PlayerBuilder().WithId(7).Build());
            session.MarkPlayerNeedsReload();

            session.SetPlayer(new PlayerBuilder().WithId(7).Build());

            Assert.False(session.PlayerNeedsReload);
        }

        [Fact]
        public void ClearSession_ResetsAPendingReloadMarker()
        {
            var session = new SessionService(new FakeSessionStore());
            session.SetPlayer(new PlayerBuilder().WithId(7).Build());
            session.MarkPlayerNeedsReload();

            session.ClearSession();

            Assert.False(session.PlayerNeedsReload);
        }

        [Fact]
        public void ClearSession_Authenticated_EvictsSessionForRecordedUser()
        {
            var store = new FakeSessionStore();
            var session = new SessionService(store);
            session.SetAuthenticatedUser(5);

            session.ClearSession();

            Assert.Equal(5, Assert.Single(store.Cleared));
        }

        [Fact]
        public void ClearSession_ExplicitUserId_EvictsThatUserEvenWhenUnauthenticated()
        {
            // The common logout path: the access token has expired, so no UserId was recorded for the
            // request, yet the user resolved from the consumed refresh token must still have their session
            // evicted (#906).
            var store = new FakeSessionStore();
            var session = new SessionService(store);

            session.ClearSession(userId: 9);

            Assert.False(session.Authenticated);
            Assert.Equal(9, Assert.Single(store.Cleared));
        }

        [Fact]
        public void ClearSession_NoUserResolvedAndUnauthenticated_EvictsNothing()
        {
            // An unknown/expired refresh token resolves to no user, so there is nothing to evict and the
            // store must not be touched.
            var store = new FakeSessionStore();
            var session = new SessionService(store);

            session.ClearSession(userId: null);

            Assert.Empty(store.Cleared);
        }

        // An in-memory session store that serves a single optional cached session and records writes, so
        // the cache-hit/miss and rehydration paths can be exercised without Redis.
        private sealed class FakeSessionStore : ISessionStore
        {
            public PlayerState? Session { get; set; }
            public List<(PlayerState State, int UserId)> Updates { get; } = [];
            public List<int> Cleared { get; } = [];
            public CancellationToken LastGetSessionToken { get; private set; }

            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default)
            {
                LastGetSessionToken = cancellationToken;
                return Task.FromResult(Session);
            }

            public void Update(PlayerState sessionData, int userId) => Updates.Add((sessionData, userId));
            public void Clear(int userId) => Cleared.Add(userId);
        }
    }
}
