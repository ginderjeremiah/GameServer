using Game.Application.Services;

namespace Game.Api.Services
{
    /// <summary>
    /// Loads the authenticated user's player session on demand. <c>SessionLoaderMiddleware</c> records the
    /// user id on every request, but the <c>GetSession</c> round-trip (and the database rehydration on a
    /// miss) is paid only where a consumer actually needs player state — the socket handshake and the
    /// Status/ActiveSession auth endpoints. Every other authenticated HTTP request (admin tooling, refresh,
    /// device-info) never touches the session cache.
    /// </summary>
    public class SessionInitializer(
        SessionService sessionService,
        AccountService accountService,
        ILogger<SessionInitializer> logger)
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly AccountService _accountService = accountService;
        private readonly ILogger<SessionInitializer> _logger = logger;

        /// <summary>
        /// Ensures the authenticated user's player session is loaded for this request: a cache read, and —
        /// when the token is valid but the cached session is gone (Redis flush, sliding-TTL lapse, or never
        /// established on this instance) — an in-memory rehydration of the user's player binding (re-derived
        /// the same way login does). The rehydration does not write the session cache; those writes belong on
        /// the socket (see <see cref="SessionService.RehydrateSession"/>). Idempotent: a no-op once a player
        /// session is present. A user with no resolvable player is left without one (the caller surfaces that
        /// as a graceful error).
        /// </summary>
        public async Task EnsureSessionLoaded(CancellationToken cancellationToken = default)
        {
            if (_sessionService.HasPlayerSession)
            {
                return;
            }

            await _sessionService.LoadPlayerState(cancellationToken);
            if (_sessionService.HasPlayerSession)
            {
                return;
            }

            var playerId = await _accountService.ResolveSelectedPlayerId(_sessionService.UserId);
            if (playerId is null)
            {
                _logger.LogWarning(
                    "Authenticated user {UserId} has a valid token but no resolvable player; session not established.",
                    _sessionService.UserId);
                return;
            }

            _logger.LogInformation("Rehydrating evicted session for authenticated user {UserId}.", _sessionService.UserId);
            _sessionService.RehydrateSession(playerId.Value);
        }
    }
}
