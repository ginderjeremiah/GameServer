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
        /// True when UserId is set to a valid authenticated user.
        /// </summary>
        public bool Authenticated => UserId > 0;

        /// <summary>
        /// Loads player state from the session store using the userId from the auth token.
        /// Called by SessionLoaderMiddleware on every authenticated request.
        /// </summary>
        public async Task LoadPlayerState(int userId)
        {
            var sessionData = await _sessionStore.GetSession(userId);
            if (sessionData is not null)
            {
                UserId = userId;
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
            PlayerState = new PlayerState { PlayerId = playerId };
            _sessionStore.Update(PlayerState, UserId);
        }

        public void SavePlayerState()
        {
            _sessionStore.Update(PlayerState, UserId);
        }
    }
}
