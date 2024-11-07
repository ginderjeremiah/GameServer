using Game.Api.Auth;
using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Core.Sessions;

namespace Game.Api.Services
{
    /// <summary>
    /// A service for loading <see cref="Session"/> data for a request.
    /// </summary>
    /// <param name="repos">The <see cref="IRepositoryManager"/> which is used to load session data.</param>
    /// <param name="cookieService"></param>
    public class SessionService(IRepositoryManager repos, CookieService cookieService)
    {
        private Session? _session;
        private readonly IRepositoryManager _repos = repos;
        private readonly CookieService _cookieService = cookieService;

        /// <summary>
        /// Indicates whether a <see cref="Session"/> has been loaded into the <see cref="SessionService"/> or not.
        /// </summary>
        public bool SessionAvailable => _session is not null;

        /// <summary>
        /// Gets the currently loaded <see cref="Session"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="SessionNotInitializedException"></exception>
        public Session GetSession()
        {
            return _session ?? throw new SessionNotInitializedException();
        }

        /// <summary>
        /// Creates a new <see cref="Session"/> for the given <paramref name="player"/>.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public async Task CreateSession(Player player)
        {
            var sessionData = await _repos.SessionStore.GetNewSessionDataAsync(player.Id);
            _session = new Session(sessionData, player, _repos);
            _cookieService.SetTokenCookie(CreateSessionToken());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task LoadSession()
        {
            var tokenString = _cookieService.GetTokenCookie();

            if (AuthToken.TryParseToken(tokenString, out var token))
            {
                await LoadSessionData(token.Claims.Sub);
                if (SessionAvailable && token.IsValid(GetSession().Player.Salt.ToString()))
                {
                    //Slide cookie if over halfway to expiration
                    if (token.Claims.Exp < DateTime.UtcNow.Add(Constants.TOKEN_LIFETIME / 2))
                    {
                        var newToken = CreateSessionToken();
                        _cookieService.SetTokenCookie(newToken);
                    }
                }
                else //clear session if invalid
                {
                    _session = null;
                }
            }
        }

        private async Task LoadSessionData(int playerId)
        {
            var sessionData = await _repos.SessionStore.GetSessionAsync(playerId);
            if (sessionData is not null)
            {
                var playerData = await _repos.Players.GetPlayer(sessionData.PlayerId);
                if (playerData is not null)
                {
                    _session = new Session(sessionData, playerData, _repos);
                }
            }
        }

        private string CreateSessionToken()
        {
            var session = GetSession();
            var claims = new AuthTokenClaims(session.Player.Id, DateTime.UtcNow + Constants.TOKEN_LIFETIME);
            var token = new AuthToken(claims, session.Player.Salt.ToString());
            return token.ToString();
        }
    }

    /// <summary>
    /// An <see cref="Exception"/> which is generated when a <see cref="SessionService"/> does not have a <see cref="Session"/> loaded.
    /// </summary>
    public class SessionNotInitializedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionNotInitializedException"/> class with the default error message.
        /// </summary>
        public SessionNotInitializedException() : base("The session was not intialized.") { }
    }
}
