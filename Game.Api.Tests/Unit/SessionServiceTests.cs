using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the synchronous in-memory player contract: the aggregate is loaded once up front (by
    /// <c>SocketInterceptorMiddleware</c> on a socket connection) and then read synchronously via
    /// <see cref="SessionService.Player"/>, so commands never re-read the cache per command.
    /// </summary>
    public class SessionServiceTests
    {
        [Fact]
        public void Player_BeforeLoad_Throws()
        {
            var session = new SessionService(new NoOpSessionStore());

            Assert.Throws<InvalidOperationException>(() => session.Player);
        }

        [Fact]
        public void Player_AfterSetPlayer_ReturnsSameInstance()
        {
            var session = new SessionService(new NoOpSessionStore());
            var player = MakePlayer(7);

            session.SetPlayer(player);

            Assert.Same(player, session.Player);
        }

        [Fact]
        public void ClearSession_ResetsLoadedPlayer()
        {
            var session = new SessionService(new NoOpSessionStore());
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
            StatPoints = new PlayerStatPoints([]) { StatPointsGained = 0, StatPointsUsed = 0 },
            Inventory = new Inventory(),
            SelectedSkills = [],
            Skills = [],
            LogPreferences = [],
        };

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }
    }
}
