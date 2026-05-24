using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application;
using Game.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerEntity = Game.Abstractions.Entities.Player;
using PlayerSkillEntity = Game.Abstractions.Entities.PlayerSkill;
using PlayerAttributeEntity = Game.Abstractions.Entities.PlayerAttribute;
using LogPreferenceEntity = Game.Abstractions.Entities.LogPreference;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class LoginController(
        IUsers users,
        IPlayers players,
        IEntityStore entityStore,
        IUnitOfWork unitOfWork,
        SessionService sessionService,
        CookieService cookieService) : ControllerBase
    {
        private readonly IUsers _users = users;
        private readonly IPlayers _players = players;
        private readonly IEntityStore _entityStore = entityStore;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
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
            var player = await _players.GetPlayer(user.Id);
            if (player is null)
            {
                return ApiResponse.Error("Player data not found");
            }

            _sessionService.CreateSession(player);

            return ApiResponse.Success(PlayerData.FromPlayer(player));
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
            var playerEntity = new PlayerEntity
            {
                Name = creds.Username,
                Level = 1,
                Exp = 0,
                CurrentZoneId = 0,
                StatPointsGained = 0,
                StatPointsUsed = 0,
            };
            _entityStore.Insert(playerEntity);

            // Commit now so EF Core generates playerEntity.Id before we attach child records.
            await _unitOfWork.CommitAsync();

            playerEntity.PlayerSkills = Enumerable.Range(0, 3).Select(id => new PlayerSkillEntity
            {
                PlayerId = playerEntity.Id,
                Selected = true,
                SkillId = id,
            }).ToList();
            playerEntity.PlayerAttributes = Enumerable.Range(0, 6).Select(id => new PlayerAttributeEntity
            {
                PlayerId = playerEntity.Id,
                AttributeId = id,
                Amount = 5m
            }).ToList();
            playerEntity.LogPreferences = [
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.Damage, Enabled = false, },
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.Debug, Enabled = false, },
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.Exp, Enabled = true, },
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.LevelUp, Enabled = true, },
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.ItemFound, Enabled = true, },
                new() { PlayerId = playerEntity.Id, LogSettingId = (int)ELogType.EnemyDefeated, Enabled = true, },
            ];

            _entityStore.Update(playerEntity);
            // CommitFilter commits the child records at end of request.

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
