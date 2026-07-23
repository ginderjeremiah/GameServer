using Game.Application.Services;

namespace Game.Api.Services
{
    /// <summary>
    /// Loads the authenticated user's player session on demand. <c>SessionLoaderMiddleware</c> records the
    /// user id (and the token's selected-player claim) on every request, but the <c>GetSession</c> round-trip
    /// (and the rehydration on a miss) is paid only where a consumer actually needs the *token's own* player
    /// state — the socket handshake and the Status auth endpoint. <c>ActiveSession</c> checks presence for an
    /// explicit target instead, so it never calls this. Every other authenticated HTTP request (admin
    /// tooling, refresh, device-info) never touches the session cache either.
    /// </summary>
    public class SessionInitializer(
        SessionService sessionService,
        ILogger<SessionInitializer> logger)
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly ILogger<SessionInitializer> _logger = logger;

        /// <summary>
        /// Ensures the authenticated user's player session is bound to the player named by the validated
        /// token's selected-player claim. The token claim is authoritative: a cached session is honoured only
        /// when it matches the claim, so an evicted cache (Redis flush, sliding-TTL lapse, never established
        /// on this instance) <em>or</em> a cache left bound to a different character (e.g. after a switch) is
        /// re-bound in memory to the token's player. The rehydration does not write the session cache; those
        /// writes belong on the socket (see <see cref="SessionService.RehydrateSession"/>). Idempotent: a
        /// no-op once the right player is bound. A pre-selection token (no player claim) leaves the request
        /// unbound (the caller surfaces that as a graceful error).
        /// </summary>
        public async Task EnsureSessionLoaded(CancellationToken cancellationToken = default)
        {
            if (_sessionService.TokenSelectedPlayerId is not int selectedPlayerId)
            {
                // A pre-selection token (post-Login, pre-SelectPlayer) is a normal, documented flow state
                // (docs/backend-auth.md), not a warning-worthy condition — every character-select screen
                // refresh would otherwise log a warning for expected behavior.
                _logger.LogDebug(
                    "Authenticated user {UserId} has a valid token with no selected player; session not established.",
                    _sessionService.UserId);
                return;
            }

            if (_sessionService.HasPlayerSession && _sessionService.SelectedPlayerId == selectedPlayerId)
            {
                return;
            }

            await _sessionService.LoadPlayerState(cancellationToken);
            if (_sessionService.HasPlayerSession && _sessionService.SelectedPlayerId == selectedPlayerId)
            {
                return;
            }

            // No cached session for this user, or one bound to a different character than the token
            // authorizes — re-bind in memory to the token's selected player.
            _logger.LogInformation("Rehydrating session for authenticated user {UserId} (player {PlayerId}).",
                _sessionService.UserId, selectedPlayerId);
            _sessionService.RehydrateSession(selectedPlayerId);
        }
    }
}
