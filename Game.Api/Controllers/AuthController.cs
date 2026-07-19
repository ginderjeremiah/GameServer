using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Api.RateLimiting;
using Game.Application.Services;
using Game.Core.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;

namespace Game.Api.Controllers
{
    /// <summary>
    /// The anonymous auth flow (Login/Refresh/CreateAccount/Logout) plus the authenticated session/status
    /// reads (Status/ActiveSession) and device telemetry (DeviceInfo) it shares presentation concerns with —
    /// split out from the character-selection flow, which lives on <see cref="PlayersController"/> (#2057).
    /// </summary>
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AuthController(
        SessionService sessionService,
        SessionInitializer sessionInitializer,
        AccountService accountService,
        LoginTrackingService loginTrackingService,
        SocketManagerService socketManager,
        PlayerService playerService,
        PlayerDataAssembler playerDataAssembler) : ControllerBase
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly SessionInitializer _sessionInitializer = sessionInitializer;
        private readonly AccountService _accountService = accountService;
        private readonly LoginTrackingService _loginTrackingService = loginTrackingService;
        private readonly SocketManagerService _socketManager = socketManager;
        private readonly PlayerService _playerService = playerService;
        private readonly PlayerDataAssembler _playerDataAssembler = playerDataAssembler;

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<LoginResult>> Login([FromBody] LoginCredentials creds)
        {
            var result = await _accountService.Login(creds.Username, creds.Password, HttpContext.RequestAborted);
            if (!result.Success)
            {
                // A backoff rejection surfaces as a 429 with a Retry-After hint (mirroring the IP rate
                // limiter's 429 shape), distinct from the 400 a plain invalid-credentials failure returns.
                if (result.Status == LoginStatus.TooManyAttempts && result.RetryAfter is TimeSpan retryAfter)
                {
                    Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    return ApiResponse.Error(LoginErrorMessage(result.Status), ApiErrorCategory.TooManyRequests);
                }

                return ApiResponse.Error(LoginErrorMessage(result.Status));
            }

            // No session binding here: login only lists the account's characters. The session is established
            // (and the token rotated to carry the chosen player) on the follow-up SelectPlayer call.
            return ApiResponse.Success(new LoginResult
            {
                Tokens = AuthTokens.From(result.Tokens),
                PlayerSummaries = result.PlayerSummaries.ToList(),
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost]
        public async Task<ApiResponse<AuthTokens>> Refresh([FromBody] RefreshRequest request)
        {
            var tokens = await _accountService.Refresh(request.RefreshToken, HttpContext.RequestAborted);
            return tokens is null
                ? ApiResponse.Error("Invalid or expired refresh token")
                : ApiResponse.Success(AuthTokens.From(tokens));
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost]
        public async Task<ApiResponse> CreateAccount([FromBody] CreateAccountRequest request)
        {
            var status = await _accountService.CreateAccount(request.Username, request.Password, HttpContext.RequestAborted);
            // Exhaustive map (mirrors the login/role mappings) so a newly-added failure status is a build-time
            // gap to fill rather than a silent success reported to the client.
            return status switch
            {
                CreateAccountStatus.Success => ApiResponse.Success(),
                CreateAccountStatus.UsernameTaken => ApiResponse.Error("There is already an account with this username."),
                CreateAccountStatus.InvalidUsername =>
                    ApiResponse.Error($"Username must be {UsernamePolicy.MinLength}-{UsernamePolicy.MaxLength} characters and contain no control or zero-width characters."),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost]
        public async Task<ApiResponse> Logout([FromBody] RefreshRequest request)
        {
            // Consuming the refresh token resolves the owning user, so the session is evicted by that id
            // even on the common path where the access token has already expired and no request principal
            // (and thus no SessionService.UserId) is present.
            var userId = await _accountService.Logout(request.RefreshToken, HttpContext.RequestAborted);
            _sessionService.ClearSession(userId);
            return ApiResponse.Success();
        }

        [HttpGet]
        public async Task<ApiResponse<PlayerData>> Status()
        {
            // Load (or rehydrate) the session so the selected player id resolves; the HTTP pipeline no
            // longer reads the session cache per request.
            await _sessionInitializer.EnsureSessionLoaded(HttpContext.RequestAborted);

            // A pre-selection token (post-Login, pre-SelectPlayer) is a normal, expected flow state, not a
            // missing-resource failure — distinguish it from a genuinely unloadable player so callers don't
            // have to infer "no character chosen yet" from an opaque 404.
            if (_sessionService.TokenSelectedPlayerId is null)
            {
                return ApiResponse.Error("No character selected.", ApiErrorCategory.NoPlayerSelected);
            }

            // A still-valid token whose player can't be loaded (deleted/archived between requests) is a
            // graceful error, not a 500 — mirroring the structured ApiResponse.Error its sibling endpoints use.
            var player = await _playerService.LoadPlayer(_sessionService.SelectedPlayerId, HttpContext.RequestAborted);
            if (player is null)
            {
                return ApiResponse.Error("Player data not found", ApiErrorCategory.NotFound);
            }

            return ApiResponse.Success(await _playerDataAssembler.Build(player, HttpContext.RequestAborted));
        }

        /// <summary>
        /// Reports whether the authenticated player already has a live game connection open elsewhere.
        /// The login flow calls this before entering the game (which would open a websocket and take over
        /// any existing session) so it can warn the user first.
        /// </summary>
        [HttpGet]
        public async Task<ApiResponse<ActiveSessionResult>> ActiveSession()
        {
            // Load (or rehydrate) the session so the selected player id resolves for the presence check;
            // the HTTP pipeline no longer reads the session cache per request.
            await _sessionInitializer.EnsureSessionLoaded(HttpContext.RequestAborted);

            var active = await _socketManager.HasActiveSocket(_sessionService.SelectedPlayerId);
            return ApiResponse.Success(new ActiveSessionResult { Active = active });
        }

        /// <summary>
        /// Records the device capabilities the frontend reports once after login, enriching the caller's own
        /// device identified by the fingerprint header of this request — a no-op if the caller has no tracked
        /// login for that fingerprint (see <see cref="LoginTrackingService.SaveDeviceInfo"/>). Requires
        /// authentication so it can only be sent by a logged-in client. Returns an error when the request
        /// carries no device fingerprint, or one that isn't shaped like the frontend's SHA-256 digest.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse> DeviceInfo([FromBody] DeviceInfoRequest request)
        {
            var fingerprint = ClientHints.DeviceFingerprint(Request.Headers);
            if (fingerprint is null)
            {
                return ApiResponse.Error("Missing device fingerprint.");
            }

            var hints = ClientHints.FromHeaders(Request.Headers);
            await _loginTrackingService.SaveDeviceInfo(
                _sessionService.UserId,
                fingerprint,
                hints.SecChUa,
                hints.SecChUaMobile,
                hints.SecChUaPlatform,
                request.DeviceMemory,
                request.HardwareConcurrency,
                HttpContext.RequestAborted);

            return ApiResponse.Success();
        }

        private static string LoginErrorMessage(LoginStatus status)
        {
            return status switch
            {
                LoginStatus.InvalidCredentials => "Invalid username or password",
                LoginStatus.Banned => "This account has been banned.",
                LoginStatus.TooManyAttempts => "Too many failed login attempts. Please wait a moment and try again.",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }
    }
}
