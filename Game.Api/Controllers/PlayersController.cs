using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Identity;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core.Players;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    /// <summary>
    /// The character-selection flow — selecting, switching, and creating characters, plus the pre-game
    /// listing/class-picker reads that flow needs — split out from the anonymous auth flow, which lives on
    /// <see cref="AuthController"/> (#2057).
    /// </summary>
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayersController(
        SessionService sessionService,
        AccountService accountService,
        CharacterSelectionService characterSelectionService) : ControllerBase
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly AccountService _accountService = accountService;
        private readonly CharacterSelectionService _characterSelectionService = characterSelectionService;

        /// <summary>
        /// Selects which of the authenticated account's characters to enter as. Validates ownership
        /// (anti-cheat), binds the session, and rotates the token pair to carry the chosen player so every
        /// later request (and the socket handshake) resolves it from the token. The active-session takeover
        /// check is made by the client after this step, since it is a per-player presence check.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse<SelectPlayerResult>> SelectPlayer([FromBody] SelectPlayerRequest request)
        {
            var outcome = await _characterSelectionService.SelectPlayer(
                _sessionService.UserId, request.PlayerId, request.RefreshToken, HttpContext.RequestAborted);
            if (!outcome.Success)
            {
                return ApiResponse.Error(SelectPlayerErrorMessage(outcome.Status), SelectPlayerErrorCategory(outcome.Status));
            }

            return ApiResponse.Success(new SelectPlayerResult { Tokens = AuthTokens.From(outcome.Tokens), Player = outcome.Player });
        }

        /// <summary>
        /// Switches the authenticated account's live character without re-logging in (spike #922). See
        /// <see cref="CharacterSelectionService.SwitchPlayer"/> for the departed-character credit and
        /// anti-cheat mechanics.
        /// </summary>
        [HttpPost]
        public async Task<ApiResponse<SelectPlayerResult>> SwitchPlayer([FromBody] SelectPlayerRequest request)
        {
            var outcome = await _characterSelectionService.SwitchPlayer(
                _sessionService.UserId, request.PlayerId, request.RefreshToken, HttpContext.RequestAborted);
            if (!outcome.Success)
            {
                return ApiResponse.Error(SelectPlayerErrorMessage(outcome.Status), SelectPlayerErrorCategory(outcome.Status));
            }

            return ApiResponse.Success(new SelectPlayerResult { Tokens = AuthTokens.From(outcome.Tokens), Player = outcome.Player });
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
            var result = await _accountService.CreatePlayer(_sessionService.UserId, request.Name, request.ClassId, HttpContext.RequestAborted);
            if (!result.Success)
            {
                return ApiResponse.Error(CreatePlayerErrorMessage(result.Status), CreatePlayerErrorCategory(result.Status));
            }

            return ApiResponse.Success(result.Player);
        }

        /// <summary>
        /// Lists the authenticated account's characters. The login flow gets these from
        /// <see cref="AuthController.Login"/>'s response, but the in-game character switcher runs inside an
        /// authenticated session with no login handoff to draw on, so it re-fetches the current list here
        /// before a switch.
        /// </summary>
        [HttpGet("/api/[controller]")]
        public async Task<ApiEnumerableResponse<PlayerSummary>> Players()
        {
            var players = await _accountService.GetPlayers(_sessionService.UserId, HttpContext.RequestAborted);
            return ApiResponse.Success(players);
        }

        /// <summary>
        /// The data the create-character class picker needs: the classes a character can be created as, each
        /// with its kit (starter skills + equipment, names resolved) and signature passive. Served over HTTP
        /// because character creation happens before a player is selected — where the socket, and the
        /// reference data it delivers, is unavailable — so it requires authentication but no selected player.
        /// Kept distinct from the reference class catalogue (`GetClasses`).
        /// </summary>
        [HttpGet]
        public ApiEnumerableResponse<CreatableClass> CharacterCreationData()
        {
            return ApiResponse.Success(_accountService.GetCreatableClasses());
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
                CreatePlayerStatus.InvalidClass => "The selected class is not available.",
                CreatePlayerStatus.CapReached => "You have reached the maximum number of characters for this account.",
                CreatePlayerStatus.UserNotFound => "Account not found",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        private static ApiErrorCategory CreatePlayerErrorCategory(CreatePlayerStatus status)
        {
            return status switch
            {
                // An invalid name, an unavailable class, and exceeding the cap are all client-side
                // validation/business failures.
                CreatePlayerStatus.InvalidName => ApiErrorCategory.BadRequest,
                CreatePlayerStatus.InvalidClass => ApiErrorCategory.BadRequest,
                CreatePlayerStatus.CapReached => ApiErrorCategory.BadRequest,
                CreatePlayerStatus.UserNotFound => ApiErrorCategory.NotFound,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }
    }
}
