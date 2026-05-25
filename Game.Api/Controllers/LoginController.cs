using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Api.Auth;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
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
        CookieService cookieService) : ControllerBase
    {
        private readonly IUsers _users = users;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IEntityStore _entityStore = entityStore;
        private readonly SessionService _sessionService = sessionService;
        private readonly CookieService _cookieService = cookieService;

        [AllowAnonymous]
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<PlayerData>> Login([FromBody] LoginCredentials creds)
        {
            if (_sessionService.SessionAvailable)
            {
                var existingPlayer = await _sessionService.LoadPlayer();
                return ApiResponse.Success(PlayerData.FromPlayer(existingPlayer));
            }

            var user = await _users.GetUser(creds.Username);
            if (user is null)
            {
                return ApiResponse.Error("Username not found");
            }

            // TODO: validate password hash against user entity

            var player = user.Players.FirstOrDefault();
            if (player is null)
            {
                return ApiResponse.Error("User has no player characters");
            }

            var playerId = player.Id;
            var playerData = await _playerRepo.GetPlayer(playerId);
            if (playerData is null)
            {
                return ApiResponse.Error("Player data not found");
            }

            _sessionService.CreateSession(user.Id, playerData);

            var token = new AuthToken(new AuthTokenClaims(user.Id, DateTime.UtcNow + Constants.TOKEN_LIFETIME));
            _cookieService.SetTokenCookie(token.ToString());

            return ApiResponse.Success(PlayerData.FromPlayer(playerData));
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
                new() { Player = playerEntity, LogSettingId = (int)ELogType.Damage, Enabled = false, },
                new() { Player = playerEntity, LogSettingId = (int)ELogType.Debug, Enabled = false, },
                new() { Player = playerEntity, LogSettingId = (int)ELogType.Exp, Enabled = true, },
                new() { Player = playerEntity, LogSettingId = (int)ELogType.LevelUp, Enabled = true, },
                new() { Player = playerEntity, LogSettingId = (int)ELogType.ItemFound, Enabled = true, },
                new() { Player = playerEntity, LogSettingId = (int)ELogType.EnemyDefeated, Enabled = true, },
            ];

            _entityStore.Insert(playerEntity);

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
    }
}
