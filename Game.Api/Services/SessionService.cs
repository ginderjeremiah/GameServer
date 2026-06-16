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
        /// token (recorded by <see cref="LoadPlayerState"/>), not from a session-cache hit — the session
        /// cache is a volatile presentation convenience, so its absence must never be mistaken for "not
        /// logged in" (see docs/backend.md → Authentication).
        /// </summary>
        public bool Authenticated => UserId > 0;

        /// <summary>
        /// True when a usable player session (a real selected player) is loaded for the authenticated user.
        /// A session-cache miss leaves this false while <see cref="Authenticated"/> stays true, signalling
        /// the caller to rehydrate the session (see <see cref="RehydrateSession"/>).
        /// </summary>
        public bool HasPlayerSession => PlayerState.PlayerId > 0;

        /// <summary>
        /// Records the authenticated user (from the validated token) and loads their in-flight player state
        /// from the session store. The user id is the sole authority for whether the caller is
        /// authenticated, so it is recorded regardless of a cache hit; a miss leaves
        /// <see cref="HasPlayerSession"/> false for the caller to rehydrate. Called by
        /// SessionLoaderMiddleware on every authenticated request.
        /// </summary>
        public async Task LoadPlayerState(int userId, CancellationToken cancellationToken = default)
        {
            UserId = userId;
            var sessionData = await _sessionStore.GetSession(userId, cancellationToken);
            if (sessionData is not null)
            {
                PlayerState = sessionData;
            }
        }

        /// <summary>
        /// Clears all session state (called when a token is invalid or on logout).
        /// </summary>
        public void ClearSession()
        {
            if (Authenticated)
            {
                _sessionStore.Clear(UserId);
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
