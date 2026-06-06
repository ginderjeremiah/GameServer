using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Api.Auth;
using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerAttributeEntity = Game.Abstractions.Entities.PlayerAttribute;
using PlayerEntity = Game.Abstractions.Entities.Player;
using PlayerSkillEntity = Game.Abstractions.Entities.PlayerSkill;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class LoginController(
        IUsers users,
        IPlayerRepository playerRepo,
        IEntityStore entityStore,
        SessionService sessionService,
        JwtTokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        LoginTrackingService loginTrackingService) : ControllerBase
    {
        private readonly IUsers _users = users;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEntityStore _entityStore = entityStore;
        private readonly SessionService _sessionService = sessionService;
        private readonly JwtTokenService _tokenService = tokenService;
        private readonly IRefreshTokenStore _refreshTokenStore = refreshTokenStore;
        private readonly LoginTrackingService _loginTrackingService = loginTrackingService;

        [AllowAnonymous]
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<LoginResult>> Login([FromBody] LoginCredentials creds)
        {
            var user = await _users.GetUser(creds.Username);
            if (user is null || !creds.Password.VerifyHash(user.Salt.ToString(), user.PassHash))
            {
                return ApiResponse.Error("Invalid username or password");
            }

            var player = user.Players.FirstOrDefault();
            if (player is null)
            {
                return ApiResponse.Error("User has no player characters");
            }

            var playerData = await _playerRepo.GetPlayer(player.Id);
            if (playerData is null)
            {
                return ApiResponse.Error("Player data not found");
            }

            _sessionService.CreateSession(user.Id, player.Id);

            var roles = user.Roles.Select(role => role.Name).ToList();
            var tokens = await IssueTokens(user.Id, roles);

            return ApiResponse.Success(new LoginResult
            {
                Tokens = tokens,
                Player = PlayerData.FromPlayer(playerData),
            });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse<AuthTokens>> Refresh([FromBody] RefreshRequest request)
        {
            var session = await _refreshTokenStore.Consume(request.RefreshToken);
            if (session is null)
            {
                return ApiResponse.Error("Invalid or expired refresh token");
            }

            var tokens = await IssueTokens(session.UserId, session.Roles);
            return ApiResponse.Success(tokens);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse> CreateAccount([FromBody] LoginCredentials creds)
        {
            var usernameTaken = await _users.CheckIfUsernameExists(creds.Username);
            if (usernameTaken)
            {
                return ApiResponse.Error("There is already an account with this username.");
            }

            var salt = Guid.NewGuid();
            var passHash = creds.Password.Hash(salt.ToString());
            var user = new User
            {
                Username = creds.Username,
                PassHash = passHash,
                Salt = salt,
                LastLogin = DateTime.UtcNow,
            };

            _entityStore.Insert(user);

            var playerEntity = new PlayerEntity
            {
                User = user,
                Name = creds.Username,
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
            };

            playerEntity.PlayerSkills = Enumerable.Range(0, 3).Select(id => new PlayerSkillEntity
            {
                Player = playerEntity,
                Selected = true,
                SkillId = id,
            }).ToList();

            playerEntity.PlayerAttributes = Enumerable.Range(0, 6).Select(id => new PlayerAttributeEntity
            {
                Player = playerEntity,
                AttributeId = id,
                Amount = 5m
            }).ToList();

            playerEntity.LogPreferences = [
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Damage, Enabled = false, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Debug, Enabled = false, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.Exp, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.LevelUp, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.ItemFound, Enabled = true, },
                new() { Player = playerEntity, LogTypeId = (int)ELogType.EnemyDefeated, Enabled = true, },
            ];

            _entityStore.Insert(playerEntity);

            return ApiResponse.Success();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse> Logout([FromBody] RefreshRequest request)
        {
            await _refreshTokenStore.Revoke(request.RefreshToken);
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

        /// <summary>
        /// Issues a fresh access/refresh token pair for the given user. The refresh token is rotated on
        /// every use (login and refresh both call this), so a previously issued refresh token is never
        /// reused.
        /// </summary>
        private async Task<AuthTokens> IssueTokens(int userId, IReadOnlyList<string> roles)
        {
            var accessToken = _tokenService.CreateAccessToken(userId, roles);
            var refreshToken = await _refreshTokenStore.Issue(userId, roles, Constants.REFRESH_TOKEN_LIFETIME);
            return new AuthTokens
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }
    }
}
