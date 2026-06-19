using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<LoginResult>> Login([FromBody] LoginCredentials creds)
        {
            var result = await _accountService.Login(creds.Username, creds.Password);
            if (!result.Success)
            {
                return ApiResponse.Error(LoginErrorMessage(result.Status));
            }

            // Session identity is a request-scoped presentation concern, so it is wired here rather than
            // in the application service.
            _sessionService.CreateSession(result.UserId, result.Player.Id);

            return ApiResponse.Success(new LoginResult
            {
                Tokens = ToAuthTokens(result.Tokens),
                Player = PlayerData.FromPlayer(result.Player),
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse<AuthTokens>> Refresh([FromBody] RefreshRequest request)
        {
            var tokens = await _accountService.Refresh(request.RefreshToken);
            return tokens is null
                ? ApiResponse.Error("Invalid or expired refresh token")
                : ApiResponse.Success(ToAuthTokens(tokens));
        }

        [AllowAnonymous]
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
                LoginStatus.NoPlayer => "User has no player characters",
                LoginStatus.PlayerDataNotFound => "Player data not found",
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
