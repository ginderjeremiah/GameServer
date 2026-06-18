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

        public int UserId { get; private set; }

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
        /// Records the authenticated user from the validated access token. The user id is the sole
        /// authority for <see cref="Authenticated"/>, so it is recorded on every authenticated request (by
        /// <c>SessionLoaderMiddleware</c>) independently of whether any player state is ever loaded.
        /// </summary>
        public void SetAuthenticatedUser(int userId)
        {
            UserId = userId;
        }

        /// <summary>
        /// Loads the authenticated user's in-flight player state from the session store. A cache miss leaves
        /// <see cref="HasPlayerSession"/> false so the caller can rehydrate it (see
        /// <see cref="SessionInitializer"/>). Only invoked where a consumer actually needs player state (the
        /// socket handshake and the Status/ActiveSession auth endpoints), never on every HTTP request.
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
            _player = null;
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
        }

        public void CreateSession(int userId, int playerId)
        {
            UserId = userId;
            EstablishSession(playerId);
        }

        /// <summary>
        /// Re-establishes a session for the already-authenticated user after its volatile cache entry was
        /// evicted (Redis flush, TTL lapse, or a session never established on this instance), binding it to
        /// the player resolved from the database and re-caching it so subsequent requests hit the cache.
        /// </summary>
        public void RehydrateSession(int playerId)
        {
            EstablishSession(playerId);
        }

        public void SavePlayerState()
        {
            _sessionStore.Update(PlayerState, UserId);
        }

        // Binds the current (authenticated) user to a fresh PlayerState for the given player and caches it.
        // Shared by the login (CreateSession) and rehydration (RehydrateSession) paths, which differ only in
        // whether UserId was already set from the token.
        private void EstablishSession(int playerId)
        {
            PlayerState = new PlayerState { PlayerId = playerId };
            _sessionStore.Update(PlayerState, UserId);
        }
    }
}
