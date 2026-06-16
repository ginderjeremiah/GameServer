using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Core.Players;
using Game.Core.Players.Inventories;
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
        public async Task LoadPlayerState_CacheHit_AuthenticatesAndLoadsPlayerSession()
        {
            var store = new FakeSessionStore { Session = new PlayerState { PlayerId = 7 } };
            var session = new SessionService(store);

            await session.LoadPlayerState(5);

            Assert.True(session.Authenticated);
            Assert.Equal(5, session.UserId);
            Assert.True(session.HasPlayerSession);
            Assert.Equal(7, session.SelectedPlayerId);
        }

        [Fact]
        public async Task LoadPlayerState_CacheMiss_AuthenticatesButHasNoPlayerSession()
        {
            // The token is the sole authority for "authenticated", so an evicted/absent session must still
            // report the user as logged in — it simply has no player session yet for the caller to rehydrate.
            var session = new SessionService(new FakeSessionStore());

            await session.LoadPlayerState(5);

            Assert.True(session.Authenticated);
            Assert.Equal(5, session.UserId);
            Assert.False(session.HasPlayerSession);
            Assert.Equal(0, session.SelectedPlayerId);
        }

        [Fact]
        public async Task RehydrateSession_EstablishesAndPersistsSession()
        {
            var store = new FakeSessionStore();
            var session = new SessionService(store);
            await session.LoadPlayerState(5);

            session.RehydrateSession(7);

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
            var player = MakePlayer(7);

            session.SetPlayer(player);

            Assert.Same(player, session.Player);
        }

        [Fact]
        public void ClearSession_ResetsLoadedPlayer()
        {
            var session = new SessionService(new FakeSessionStore());
            session.SetPlayer(MakePlayer(7));

            session.ClearSession();

            Assert.Throws<InvalidOperationException>(() => session.Player);
        }

        private static Player MakePlayer(int id) => new()
        {
            Id = id,
            Name = "Test",
            Level = 1,
            Exp = 0,
            CurrentZoneId = 0,
            StatPoints = new PlayerStatPoints { StatAllocations = [], StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };

        // An in-memory session store that serves a single optional cached session and records writes, so
        // the cache-hit/miss and rehydration paths can be exercised without Redis.
        private sealed class FakeSessionStore : ISessionStore
        {
            public PlayerState? Session { get; set; }
            public List<(PlayerState State, int UserId)> Updates { get; } = [];

            public Task<PlayerState?> GetSession(int userId) => Task.FromResult(Session);
            public void Update(PlayerState sessionData, int userId) => Updates.Add((sessionData, userId));
            public void Clear(int userId) { }
        }
    }
}
