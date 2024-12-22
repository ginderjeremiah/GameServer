using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Core;
using Game.Core.DataAccess;
using Game.Core.Entities;
using Game.Core.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly SessionService _sessionService;

        private Session Session => _sessionService.GetSession();

        public LoginController(IRepositoryManager repositoryManager, SessionService sessionService)
        {
            _repositoryManager = repositoryManager;
            _sessionService = sessionService;
        }

        [AllowAnonymous]
        [HttpPost("/api/[controller]")]
        public async Task<ApiResponse<PlayerData>> Login([FromBody] LoginCredentials creds)
        {
            if (_sessionService.SessionAvailable)
            {
                return ApiResponse.Success(Session.GetPlayerData());
            }

            var player = await _repositoryManager.Players.GetPlayer(creds.Username);
            if (player is null)
            {
                return ApiResponse.Error("Username not found");
            }

            var passHash = creds.Password.Hash(player.Salt.ToString());
            if (passHash != player.PassHash)
            {
                return ApiResponse.Error("Username or password is incorrect");
            }

            _sessionService.CreateSession(player);

            return ApiResponse.Success(Session.GetPlayerData());
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse> CreateAccount([FromBody] LoginCredentials creds)
        {
            var usernameTaken = await _repositoryManager.Players.CheckIfUsernameExists(creds.Username);
            if (usernameTaken)
            {
                return ApiResponse.Error("There is already an account with this username.");
            }

            var salt = Guid.NewGuid();
            var passHash = creds.Password.Hash(salt.ToString());
            var player = new Player
            {
                UserName = creds.Username,
                Salt = salt,
                PassHash = passHash,
                Level = 1,
                Name = creds.Username,
                StatPointsGained = 0,
                StatPointsUsed = 0,
            };
            _repositoryManager.Insert(player);
            await _repositoryManager.SaveChangesAsync();

            player.PlayerSkills = Enumerable.Range(0, 3).Select(id => new PlayerSkill
            {
                PlayerId = player.Id,
                Selected = true,
                SkillId = id,
            }).ToList();
            player.PlayerAttributes = Enumerable.Range(0, 6).Select(id => new PlayerAttribute
            {
                PlayerId = player.Id,
                AttributeId = id,
                Amount = 5m
            }).ToList();
            player.LogPreferences = [
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.Damage, Enabled = false, },
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.Debug, Enabled = false, },
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.Exp, Enabled = true, },
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.LevelUp, Enabled = true, },
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.Inventory, Enabled = true, },
                new() { PlayerId = player.Id, LogSettingId = (int)ELogSetting.EnemyDefeated, Enabled = true, },
            ];

            _repositoryManager.Update(player);
            await _repositoryManager.SaveChangesAsync();

            return ApiResponse.Success();
        }

        [HttpGet]
        public ApiResponse<PlayerData> Status()
        {
            return _sessionService.SessionAvailable ? ApiResponse.Success(Session.GetPlayerData()) : ApiResponse.Error("Not logged in");
        }
    }
}