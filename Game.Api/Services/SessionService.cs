using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.Api.Services
{
    /// <summary>
    /// Manages authentication state and player identity for the current request.
    /// This is a presentation-layer concern (cookies, tokens, session identity).
    /// </summary>
    public class SessionService(ISessionStore sessionStore)
    {
        private readonly ISessionStore _sessionStore = sessionStore;
        private Player? _player;
        private bool _playerNeedsReload;

        public int UserId { get; private set; }

        /// <summary>
        /// The player id carried by the validated access token's selected-player claim, or
        /// <see langword="null"/> on a pre-selection token. This is the authority for which character the
        /// request binds to (see <see cref="SessionInitializer"/>), independent of whatever the volatile
        /// session cache happens to hold.
        /// </summary>
        public int? TokenSelectedPlayerId { get; private set; }

        public int SelectedPlayerId => PlayerState.PlayerId;

        public PlayerState PlayerState { get; private set; } = new();

        /// <summary>
        /// True when the request carries a valid authenticated user. Derived from the validated access
        /// token (recorded by <see cref="SetAuthenticatedUser"/>), not from a session-cache hit — the
        /// session cache is a volatile presentation convenience, so its absence must never be mistaken for
        /// "not logged in" (see docs/backend.md → Authentication).
        /// </summary>
        public bool Authenticated => UserId > 0;

        /// <summary>
        /// True when a usable player session (a real selected player) is loaded for the authenticated user.
        /// A session-cache miss leaves this false while <see cref="Authenticated"/> stays true, signalling
        /// the caller to rehydrate the session (see <see cref="RehydrateSession"/>).
        /// </summary>
        public bool HasPlayerSession => PlayerState.PlayerId > 0;

        /// <summary>
        /// Records the authenticated user (and the selected player carried by the token, if any) from the
        /// validated access token. The user id is the sole authority for <see cref="Authenticated"/>, so it
        /// is recorded on every authenticated request (by <c>SessionLoaderMiddleware</c>) independently of
        /// whether any player state is ever loaded.
        /// </summary>
        public void SetAuthenticatedUser(int userId, int? selectedPlayerId = null)
        {
            UserId = userId;
            TokenSelectedPlayerId = selectedPlayerId;
        }

        /// <summary>
        /// Loads the authenticated user's in-flight player state from the session store. A cache miss leaves
        /// <see cref="HasPlayerSession"/> false so the caller can rehydrate it (see
        /// <see cref="SessionInitializer"/>). Only invoked where a consumer actually needs the *token's own*
        /// player state (the socket handshake and the Status auth endpoint), never on every HTTP request.
        /// </summary>
        public async Task LoadPlayerState(CancellationToken cancellationToken = default)
        {
            var sessionData = await _sessionStore.GetSession(UserId, cancellationToken);
            if (sessionData is not null)
            {
                PlayerState = sessionData;
            }
        }

        /// <summary>
        /// Clears all session state (called when a token is invalid or on logout). The cached session is
        /// evicted for <paramref name="userId"/> when supplied (e.g. the user resolved from a consumed
        /// refresh token on logout), otherwise for the token-recorded <see cref="UserId"/>. The explicit id
        /// is what makes logout deterministically evict the session even when the access token has already
        /// expired and no <see cref="UserId"/> was recorded for the request.
        /// </summary>
        public void ClearSession(int? userId = null)
        {
            var sessionUserId = userId ?? (Authenticated ? UserId : (int?)null);
            if (sessionUserId is int id)
            {
                _sessionStore.Clear(id);
            }

            UserId = 0;
            TokenSelectedPlayerId = null;
            _player = null;
            _playerNeedsReload = false;
            PlayerState = new();
        }

        /// <summary>
        /// The player aggregate for this session. On a socket connection it is loaded once up front when the
        /// socket connects (<c>SocketInterceptorMiddleware</c>) and held in memory for the connection's
        /// lifetime, so socket commands read it synchronously and the connection never re-reads the cache per
        /// command (see docs/backend-persistence.md -> Caching and Pub/Sub). Throws if accessed before <see cref="SetPlayer"/>.
        /// </summary>
        public Player Player => _player
            ?? throw new InvalidOperationException("Player has not been loaded for this session.");

        /// <summary>Stores the loaded player aggregate on the session for synchronous access by commands.</summary>
        public void SetPlayer(Player player)
        {
            _player = player;
            _playerNeedsReload = false;
        }

        /// <summary>
        /// True once a command's <c>SavePlayer</c> flush has genuinely failed (see
        /// <see cref="Game.Abstractions.DataAccess.PlayerPersistenceFlushFailedException"/>) — the in-memory
        /// <see cref="Player"/> may hold mutations that never reached the write-behind queue. <c>SocketHandler</c>
        /// reloads the player from the repository before the next command runs whenever this is set, converging
        /// the session back onto the last successfully-persisted state instead of silently carrying the stuck
        /// mutation forward (#1632).
        /// </summary>
        public bool PlayerNeedsReload => _playerNeedsReload;

        public void MarkPlayerNeedsReload()
        {
            _playerNeedsReload = true;
        }

        /// <summary>
        /// Establishes the session binding for <paramref name="playerId"/> (login's <c>SelectPlayer</c>, or an
        /// in-game <c>SwitchPlayer</c>) and primes the session cache here — before any socket exists — so the
        /// subsequent Status/socket reads hit the cache instead of rehydrating. When the cache already holds
        /// state for this same player (a credential re-login, or switching back to the character already bound),
        /// that cached state is kept rather than overwritten, since it may carry a cache-only in-flight battle
        /// snapshot that exists nowhere else. A genuinely different (or absent) cached player still gets a
        /// fresh <see cref="PlayerState"/>.
        /// </summary>
        public async Task CreateSession(int userId, int playerId, CancellationToken cancellationToken = default)
        {
            UserId = userId;
            var existing = await _sessionStore.GetSession(userId, cancellationToken);
            PlayerState = existing is not null && existing.PlayerId == playerId
                ? existing
                : new PlayerState { PlayerId = playerId };
            _sessionStore.Update(PlayerState, UserId);
        }

        /// <summary>
        /// Re-binds the already-authenticated user to its player <em>in memory only</em> after a session-cache
        /// miss (Redis flush, sliding-TTL lapse, or a session never established on this instance). It deliberately
        /// does not write the session cache: that write would land on the concurrent HTTP path, and player-state
        /// writes belong on the socket where they serialize with the battle loop (see docs/backend.md → HTTP vs
        /// WebSocket). The cache is (re)populated by the socket's write-behind path; until then the binding is
        /// simply re-derived per request, which is cheap on the auth flow this runs on.
        /// </summary>
        public void RehydrateSession(int playerId)
        {
            PlayerState = new PlayerState { PlayerId = playerId };
        }

        /// <summary>
        /// Persists the in-flight <see cref="PlayerState"/> to the session cache — awaited, not fire-and-forget,
        /// because this is the reconnect/rehydration path's source of truth for the in-flight battle. The
        /// battle-lifecycle commands call this <em>after</em> durably crediting a battle's outcome and clearing
        /// it off <see cref="PlayerState"/>; a dropped write here would leave the stale session showing the
        /// already-credited battle as still active, so a reconnect's next battle-end would re-credit it.
        /// </summary>
        public async Task SavePlayerStateAsync(CancellationToken cancellationToken = default)
        {
            await _sessionStore.UpdateAsync(PlayerState, UserId, cancellationToken);
        }
    }
}
