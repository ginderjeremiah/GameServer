using Game.Abstractions.DataAccess;
using Game.Core.Players;

namespace Game.Api.Services
{
    /// <summary>
    /// Manages authentication state and player identity for the current request.
    /// This is a presentation-layer concern (cookies, tokens, session identity).
    /// </summary>
    public class SessionService(IPlayers playerRepo, ISessionStore sessionStore)
    {
        private readonly IPlayers _playerRepo = playerRepo;
        private readonly ISessionStore _sessionStore = sessionStore;
        private Player? _player;

        public string SessionId { get; set; } = string.Empty;

        public int UserId { get; private set; }

        public int SelectedPlayerId => PlayerState.PlayerId;

        public PlayerState PlayerState { get; private set; } = new();

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
            var sessionData = await _sessionStore.GetSession(userId.ToString());
            if (sessionData is not null)
            {
                UserId = userId;
                PlayerState = sessionData;
            }
        }

        /// <summary>
        /// Clears all session state (called when a token is invalid).
        /// </summary>
        public void ClearSession()
        {
            UserId = 0;
            _player = null;
            PlayerState = new();
        }

        public async Task LoadSession(int userId, string sessionId)
        {
            UserId = userId;
            var sessionData = await _sessionStore.GetSession(sessionId);
            if (sessionData is not null)
            {
                PlayerState = sessionData;
            }
        }

        public async Task<Player> LoadPlayer()
        {
            return _player ??= await _playerRepo.GetPlayer(SelectedPlayerId)
                ?? throw new InvalidOperationException("Player data not loaded.");
        }

        public void CreateSession(Player player)
        {
            _player = player;
            PlayerState = new PlayerState { PlayerId = player.Id };
        }

        public void SavePlayerState()
        {
            _sessionStore.Update(PlayerState, SelectedPlayerId);
        }
    }
}
