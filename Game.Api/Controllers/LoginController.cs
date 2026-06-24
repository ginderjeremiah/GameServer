using Game.Abstractions.Contracts.Identity;
using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Api.RateLimiting;
using Game.Application.Services;
using Game.Core.Players;
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
        PlayerService playerService,
        BattleService battleService,
        ILogger<LoginController> logger) : ControllerBase
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly SessionInitializer _sessionInitializer = sessionInitializer;
        private readonly AccountService _accountService = accountService;
        private readonly LoginTrackingService _loginTrackingService = loginTrackingService;
        private readonly SocketManagerService _socketManager = socketManager;
        private readonly PlayerService _playerService = playerService;
        private readonly BattleService _battleService = battleService;
        private readonly ILogger<LoginController> _logger = logger;

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

        /// <summary>
        /// Switches the authenticated account's live character without re-logging in (spike #922). Credits the
        /// departed character — the one the caller's token currently binds — for any elapsed idle time via the
        /// offline-rewards simulator, resolving its in-flight battle and dropping the login-time 5-minute floor
        /// so a deliberate switch loses no progress, then binds and loads the newly selected character through
        /// the same path as <see cref="SelectPlayer"/> (validating ownership and rotating the token). The client
        /// must tear down its game socket before calling this, since the departed-character credit runs over
        /// HTTP, off that character's battle loop; the credit is skipped server-side if that socket is still
        /// live, so a misbehaving client cannot race the credit against the live loop's saves.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse<SelectPlayerResult>> SwitchPlayer([FromBody] SelectPlayerRequest request)
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            // Validate the switch target up front (read-only, no token rotation) so an unowned switch never
            // credits or mutates the departed character. SelectPlayer below re-validates and may also fail on an
            // invalid refresh token, in which case the departed character has been credited and re-anchored (benign:
            // the progress is legitimate and re-anchoring makes a retry near-idempotent).
            if (!await _accountService.OwnsPlayer(_sessionService.UserId, request.PlayerId))
            {
                return ApiResponse.Error(SelectPlayerErrorMessage(SelectPlayerStatus.NotOwned), SelectPlayerErrorCategory(SelectPlayerStatus.NotOwned));
            }

            // Settle the departed character before binding the new one, so its idle progress and in-flight
            // battle are credited rather than discarded when the session is rebound below.
            await CreditDepartedCharacter(request.PlayerId, HttpContext.RequestAborted);

            var result = await _accountService.SelectPlayer(_sessionService.UserId, request.PlayerId, request.RefreshToken);
            if (!result.Success)
            {
                return ApiResponse.Error(SelectPlayerErrorMessage(result.Status), SelectPlayerErrorCategory(result.Status));
            }

            _sessionService.CreateSession(_sessionService.UserId, result.Player.Id);

            return ApiResponse.Success(new SelectPlayerResult
            {
                Tokens = ToAuthTokens(result.Tokens),
                Player = PlayerData.FromPlayer(result.Player),
            });
        }

        /// <summary>
        /// Credits the character the caller is switching away from (the token's currently-selected player) for
        /// any elapsed idle time, resolving its in-flight battle. A no-op when there is no departed character to
        /// settle — a pre-selection token (no bound player) or a switch to the same character — when that
        /// character can no longer be loaded, or when it still has a live socket (its battle loop owns its saves,
        /// so crediting it here would race that loop — see the server-side guard below).
        /// </summary>
        private async Task CreditDepartedCharacter(int targetPlayerId, CancellationToken cancellationToken)
        {
            if (_sessionService.TokenSelectedPlayerId is not int departedPlayerId || departedPlayerId == targetPlayerId)
            {
                return;
            }

            // The credit is a read-modify-write against the departed character's aggregate, run here over HTTP
            // off its battle loop. If that character still has a live socket, its battle-completion commands are
            // mutating the same cached aggregate under the per-socket command lock — crediting it here would
            // reintroduce the exact lost-update race that lock exists to prevent. The client is expected to tear
            // its game socket down before switching, so a live socket means a misbehaving/malicious client: skip
            // the credit (the live loop owns its own saves) and proceed with the switch rather than racing it.
            if (await _socketManager.HasActiveSocket(departedPlayerId))
            {
                _logger.LogWarning(
                    "Skipping the switch-away credit for player {DepartedPlayerId}: it still has a live socket, so its battle loop owns its saves. The client should close its game socket before switching.",
                    departedPlayerId);
                return;
            }

            // Bind the departed character's in-flight session state (from the token claim) so the simulator can
            // resolve any stale battle, then load its aggregate to apply the credited rewards to.
            await _sessionInitializer.EnsureSessionLoaded(cancellationToken);
            var departed = await _playerService.LoadPlayer(departedPlayerId, cancellationToken);
            if (departed is null)
            {
                return;
            }

            await _battleService.SimulateSwitchProgress(departed, _sessionService.PlayerState, cancellationToken);
        }

        /// <summary>
        /// Creates an additional character on the authenticated account. Validates the name and enforces the
        /// per-account character cap server-side (anti-cheat). Runs over HTTP as part of the pre-game
        /// character-select flow — no session binding is established, so the caller's selected character is
        /// unchanged. Returns the new character's summary so the client can add it to the select list.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse<PlayerSummary>> CreatePlayer([FromBody] CreatePlayerRequest request)
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            var result = await _accountService.CreatePlayer(_sessionService.UserId, request.Name);
            if (!result.Success)
            {
                return ApiResponse.Error(CreatePlayerErrorMessage(result.Status), CreatePlayerErrorCategory(result.Status));
            }

            return ApiResponse.Success(result.Player);
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
        [EnableRateLimiting(RateLimitingOptions.AuthPolicy)]
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
        /// Lists the authenticated account's characters. The login flow gets these from <see cref="Login"/>'s
        /// response, but the in-game character switcher runs inside an authenticated session with no login
        /// handoff to draw on, so it re-fetches the current list here before a switch.
        /// </summary>
        [HttpGet]
        public async Task<ApiEnumerableResponse<PlayerSummary>> Players()
        {
            if (!_sessionService.Authenticated)
            {
                return ApiResponse.Error("Not logged in", ApiErrorCategory.Unauthorized);
            }

            var players = await _accountService.GetPlayers(_sessionService.UserId);
            return ApiResponse.Success(players);
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

        private static string CreatePlayerErrorMessage(CreatePlayerStatus status)
        {
            return status switch
            {
                CreatePlayerStatus.InvalidName =>
                    $"Character names must be {PlayerName.MinLength}-{PlayerName.MaxLength} characters and contain no control characters.",
                CreatePlayerStatus.CapReached => "You have reached the maximum number of characters for this account.",
                CreatePlayerStatus.UserNotFound => "Account not found",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        private static ApiErrorCategory CreatePlayerErrorCategory(CreatePlayerStatus status)
        {
            return status switch
            {
                // Both an invalid name and exceeding the cap are client-side validation/business failures.
                CreatePlayerStatus.InvalidName => ApiErrorCategory.BadRequest,
                CreatePlayerStatus.CapReached => ApiErrorCategory.BadRequest,
                CreatePlayerStatus.UserNotFound => ApiErrorCategory.NotFound,
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
