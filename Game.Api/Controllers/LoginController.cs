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
        AccountService accountService,
        LoginTrackingService loginTrackingService) : ControllerBase
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly AccountService _accountService = accountService;
        private readonly LoginTrackingService _loginTrackingService = loginTrackingService;

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
            return status == CreateAccountStatus.UsernameTaken
                ? ApiResponse.Error("There is already an account with this username.")
                : ApiResponse.Success();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse> Logout([FromBody] RefreshRequest request)
        {
            await _accountService.Logout(request.RefreshToken);
            _sessionService.ClearSession();
            return ApiResponse.Success();
        }

        [HttpGet]
        public async Task<ApiResponse<PlayerData>> Status()
        {
            if (!_sessionService.SessionAvailable)
            {
                return ApiResponse.Error("Not logged in");
            }

            var player = await _sessionService.LoadPlayer();
            return ApiResponse.Success(PlayerData.FromPlayer(player));
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
                request.HardwareConcurrency);

            return ApiResponse.Success();
        }

        private static string LoginErrorMessage(LoginStatus status)
        {
            return status switch
            {
                LoginStatus.NoPlayer => "User has no player characters",
                LoginStatus.PlayerDataNotFound => "Player data not found",
                _ => "Invalid username or password",
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
