using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core.Players;

namespace Game.Api.Services
{
    /// <summary>
    /// Manages authentication state and player identity for the current request.
    /// This is a presentation-layer concern (cookies, tokens, session identity).
    /// </summary>
    public class SessionService(PlayerService playerService, ISessionStore sessionStore)
    {
        private readonly PlayerService _playerService = playerService;
        private readonly ISessionStore _sessionStore = sessionStore;
        private Player? _player;

        public string SessionId { get; set; } = string.Empty;

        public int UserId { get; private set; }

        public int SelectedPlayerId => PlayerState.PlayerId;

        public PlayerState PlayerState { get; private set; } = new();

        /// <summary>
        /// The access roles granted to the authenticated user, sourced from the auth token claims.
        /// </summary>
        public IReadOnlyList<string> Roles { get; private set; } = [];

        public bool SessionAvailable => UserId > 0;

        /// <summary>
        /// True when UserId is set to a valid authenticated user.
        /// </summary>
        public bool Authenticated => UserId > 0;

        /// <summary>
        /// Loads player state from the session store using the userId from the auth token.
        /// Called by TokenAuthMiddleware on every authenticated request.
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
        /// Sets the access roles for the current request from the auth token claims.
        /// Called by TokenAuthMiddleware once a token has been validated.
        /// </summary>
        public void SetRoles(IReadOnlyList<string> roles)
        {
            Roles = roles;
        }

        /// <summary>
        /// Determines whether the authenticated user has been granted the given role.
        /// </summary>
        public bool IsInRole(string role)
        {
            return Roles.Contains(role);
        }

        /// <summary>
        /// Clears all session state (called when a token is invalid or on logout).
        /// </summary>
        public void ClearSession()
        {
            if (SessionAvailable)
            {
                _sessionStore.Clear(UserId);
            }

            UserId = 0;
            _player = null;
            PlayerState = new();
            Roles = [];
        }

        public async Task<Player> LoadPlayer()
        {
            return _player ??= await _playerService.LoadPlayer(SelectedPlayerId)
                ?? throw new InvalidOperationException("Player data not loaded.");
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

        public void ClearPlayerDomainEvents()
        {
            _player?.ClearEvents();
        }
    }
}
