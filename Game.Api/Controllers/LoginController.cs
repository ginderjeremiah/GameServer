using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Api.RateLimiting;
using Game.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class LoginController(
        SessionService sessionService,
        SessionInitializer sessionInitializer,
        AccountService accountService,
        LoginTrackingService loginTrackingService,
        SocketManagerService socketManager,
        PlayerService playerService) : ControllerBase
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly SessionInitializer _sessionInitializer = sessionInitializer;
        private readonly AccountService _accountService = accountService;
        private readonly LoginTrackingService _loginTrackingService = loginTrackingService;
        private readonly SocketManagerService _socketManager = socketManager;
        private readonly PlayerService _playerService = playerService;

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<LoginResult>> Login([FromBody] LoginCredentials creds)
        {
            var result = await _accountService.Login(creds.Username, creds.Password);
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
                Tokens = ToAuthTokens(result.Tokens),
                PlayerSummaries = result.PlayerSummaries.ToList(),
            });
        }

        /// <summary>
        /// Selects which of the authenticated account's characters to enter as. Validates ownership
        /// (anti-cheat), binds the session, and rotates the token pair to carry the chosen player so every
        /// later request (and the socket handshake) resolves it from the token. The active-session takeover
        /// check is made by the client after this step, since it is a per-player presence check.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse<SelectPlayerResult>> SelectPlayer([FromBody] SelectPlayerRequest request)
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            var result = await _accountService.SelectPlayer(_sessionService.UserId, request.PlayerId, request.RefreshToken);
            if (!result.Success)
            {
                return ApiResponse.Error(SelectPlayerErrorMessage(result.Status), SelectPlayerErrorCategory(result.Status));
            }

            // Establish the session binding now that a character is chosen — a request-scoped presentation
            // concern, mirroring how login used to bind. The token returned already carries the player.
            _sessionService.CreateSession(_sessionService.UserId, result.Player.Id);

            return ApiResponse.Success(new SelectPlayerResult
            {
                Tokens = ToAuthTokens(result.Tokens),
                Player = PlayerData.FromPlayer(result.Player),
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost]
        public async Task<ApiResponse<AuthTokens>> Refresh([FromBody] RefreshRequest request)
        {
            var tokens = await _accountService.Refresh(request.RefreshToken);
            return tokens is null
                ? ApiResponse.Error("Invalid or expired refresh token")
                : ApiResponse.Success(ToAuthTokens(tokens));
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
        [HttpPost]
        public async Task<ApiResponse> CreateAccount([FromBody] LoginCredentials creds)
        {
            var status = await _accountService.CreateAccount(creds.Username, creds.Password);
            // Exhaustive map (mirrors the login/role mappings) so a newly-added failure status is a build-time
            // gap to fill rather than a silent success reported to the client.
            return status switch
            {
                CreateAccountStatus.Success => ApiResponse.Success(),
                CreateAccountStatus.UsernameTaken => ApiResponse.Error("There is already an account with this username."),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse> Logout([FromBody] RefreshRequest request)
        {
            // Consuming the refresh token resolves the owning user, so the session is evicted by that id
            // even on the common path where the access token has already expired and no request principal
            // (and thus no SessionService.UserId) is present.
            var userId = await _accountService.Logout(request.RefreshToken);
            _sessionService.ClearSession(userId);
            return ApiResponse.Success();
        }

        [HttpGet]
        public async Task<ApiResponse<PlayerData>> Status()
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            // Load (or rehydrate) the session so the selected player id resolves; the HTTP pipeline no
            // longer reads the session cache per request.
            await _sessionInitializer.EnsureSessionLoaded(HttpContext.RequestAborted);

            // A still-valid token whose player can't be loaded (deleted/archived between requests) is a
            // graceful error, not a 500 — mirroring the structured ApiResponse.Error its sibling endpoints use.
            var player = await _playerService.LoadPlayer(_sessionService.SelectedPlayerId);
            if (player is null)
            {
                return ApiResponse.Error("Player data not found", ApiErrorCategory.NotFound);
            }

            return ApiResponse.Success(PlayerData.FromPlayer(player));
        }

        /// <summary>
        /// Reports whether the authenticated player already has a live game connection open elsewhere.
        /// The login flow calls this before entering the game (which would open a websocket and take over
        /// any existing session) so it can warn the user first.
        /// </summary>
        [HttpGet]
        public async Task<ApiResponse<ActiveSessionResult>> ActiveSession()
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            // Load (or rehydrate) the session so the selected player id resolves for the presence check;
            // the HTTP pipeline no longer reads the session cache per request.
            await _sessionInitializer.EnsureSessionLoaded(HttpContext.RequestAborted);

            var active = await _socketManager.HasActiveSocket(_sessionService.SelectedPlayerId);
            return ApiResponse.Success(new ActiveSessionResult { Active = active });
        }

        /// <summary>
        /// Records the device capabilities the frontend reports once after login, enriching the device
        /// identified by the fingerprint header of this request. Requires authentication so it can only be
        /// sent by a logged-in client. Returns an error when the request carries no device fingerprint.
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
                fingerprint,
                hints.UserAgent,
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

        private static string SelectPlayerErrorMessage(SelectPlayerStatus status)
        {
            return status switch
            {
                SelectPlayerStatus.InvalidToken => "Your session is no longer valid. Please log in again.",
                SelectPlayerStatus.NotOwned => "That character does not belong to your account.",
                SelectPlayerStatus.PlayerDataNotFound => "Player data not found",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        private static ApiErrorCategory SelectPlayerErrorCategory(SelectPlayerStatus status)
        {
            return status switch
            {
                // An invalid/foreign refresh token means the caller must re-authenticate.
                SelectPlayerStatus.InvalidToken => ApiErrorCategory.Unauthorized,
                // A legit client never selects an unowned character; treat tampering as a plain bad request.
                SelectPlayerStatus.NotOwned => ApiErrorCategory.BadRequest,
                SelectPlayerStatus.PlayerDataNotFound => ApiErrorCategory.NotFound,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        private static AuthTokens ToAuthTokens(AuthTokenPair tokens)
        {
            return new AuthTokens
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
            };
        }
    }
}
