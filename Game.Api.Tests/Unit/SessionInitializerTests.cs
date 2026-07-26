using Game.Api.Services;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the two session-binding modes <c>SocketInterceptorMiddleware</c> depends on: the ordinary
    /// <see cref="SessionInitializer.EnsureSessionLoaded"/> is idempotent (a bound session is never re-read),
    /// while <see cref="SessionInitializer.ReloadSession"/> deliberately discards what is bound and re-reads
    /// the store — the handshake's post-registration convergence onto a switch-away credit's result (#2463).
    /// Both keep the token's selected-player claim authoritative over whatever the cache happens to hold.
    /// </summary>
    public class SessionInitializerTests
    {
        private const int UserId = 5;
        private const int PlayerId = 7;

        [Fact]
        public async Task EnsureSessionLoaded_AlreadyBoundToTheTokensPlayer_DoesNotReadTheStoreAgain()
        {
            // The idempotence every non-socket caller relies on: a request that already resolved its player
            // must not pay a second GetSession round trip.
            var (store, session, initializer) = CreateInitializer();
            store.Session = new PlayerState { PlayerId = PlayerId };
            await initializer.EnsureSessionLoaded();
            Assert.Equal(1, store.GetSessionCalls);

            await initializer.EnsureSessionLoaded();

            Assert.Equal(1, store.GetSessionCalls);
            Assert.Equal(PlayerId, session.SelectedPlayerId);
        }

        [Fact]
        public async Task ReloadSession_AlreadyBoundToTheTokensPlayer_ReplacesItWithTheStoresCurrentState()
        {
            // The switch-away credit resolves the departed character's in-flight battle and clears it off the
            // session state. A connection that bound the pre-credit state must converge onto the settled one,
            // or its first battle-end re-credits a fight the credit already paid out.
            var (store, session, initializer) = CreateInitializer();
            store.Session = ActiveBattleState();
            await initializer.EnsureSessionLoaded();
            Assert.True(session.PlayerState.HasActiveBattle);

            store.Session = new PlayerState { PlayerId = PlayerId };
            await initializer.ReloadSession();

            Assert.Equal(PlayerId, session.SelectedPlayerId);
            Assert.False(session.PlayerState.HasActiveBattle);
        }

        [Fact]
        public async Task ReloadSession_StoreNowBoundToADifferentCharacter_RebindsFreshToTheTokensPlayer()
        {
            // Session_{userId} is account-keyed, so a completed switch leaves it holding the *new* character's
            // state. The token claim stays authoritative: this connection is rebound to its own player rather
            // than inheriting the other character's battle.
            var (store, session, initializer) = CreateInitializer();
            store.Session = ActiveBattleState();
            await initializer.EnsureSessionLoaded();

            store.Session = ActiveBattleState(playerId: PlayerId + 1);
            await initializer.ReloadSession();

            Assert.Equal(PlayerId, session.SelectedPlayerId);
            Assert.False(session.PlayerState.HasActiveBattle);
        }

        [Fact]
        public async Task ReloadSession_StoreHasNoSession_DiscardsTheStaleBoundState()
        {
            // LoadPlayerState keeps whatever is bound on a cache miss, so a forced reload has to drop the old
            // state itself — otherwise an evicted session would silently leave the very snapshot this reload
            // exists to discard in place.
            var (store, session, initializer) = CreateInitializer();
            store.Session = ActiveBattleState();
            await initializer.EnsureSessionLoaded();
            Assert.True(session.PlayerState.HasActiveBattle);

            store.Session = null;
            await initializer.ReloadSession();

            Assert.Equal(PlayerId, session.SelectedPlayerId);
            Assert.False(session.PlayerState.HasActiveBattle);
        }

        [Fact]
        public async Task ReloadSession_PreSelectionToken_LeavesTheRequestUnbound()
        {
            // No selected-player claim means there is nothing to converge onto; the caller surfaces that as a
            // graceful error rather than this rebinding to an arbitrary cached character.
            var store = new FakeSessionStore { Session = ActiveBattleState() };
            var session = new SessionService(store);
            session.SetAuthenticatedUser(UserId);
            var initializer = new SessionInitializer(session, NullLogger<SessionInitializer>.Instance);

            await initializer.ReloadSession();

            Assert.False(session.HasPlayerSession);
            Assert.Equal(0, store.GetSessionCalls);
        }

        private static (FakeSessionStore Store, SessionService Session, SessionInitializer Initializer) CreateInitializer()
        {
            var store = new FakeSessionStore();
            var session = new SessionService(store);
            session.SetAuthenticatedUser(UserId, PlayerId);
            return (store, session, new SessionInitializer(session, NullLogger<SessionInitializer>.Instance));
        }

        /// <summary>A session state carrying an in-flight battle — the part a switch-away credit settles.</summary>
        private static PlayerState ActiveBattleState(int playerId = PlayerId)
        {
            return new PlayerState
            {
                PlayerId = playerId,
                ActiveEnemyId = 1,
                ActiveEnemyLevel = 1,
                BattleSeed = 1234u,
                BattleStartTime = DateTime.UtcNow,
            };
        }
    }
}
