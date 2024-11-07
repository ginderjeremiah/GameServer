using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Core;
using Game.Core.DataAccess;
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
                return ApiResponse.Success(Session.GetPlayerData());

            var player = await _repositoryManager.Players.GetPlayer(creds.Username);

            if (player is null)
                return ApiResponse.Error<PlayerData>("Username not found");

            var passHash = creds.Password.Hash(player.Salt.ToString());

            if (passHash != player.PassHash)
                return ApiResponse.Error<PlayerData>("Username or password is incorrect");

            await _sessionService.CreateSession(player);

            return ApiResponse.Success(Session.GetPlayerData());
        }

        [HttpGet]
        public ApiResponse Status()
        {
            return _sessionService.SessionAvailable ? ApiResponse.Success() : ApiResponse.Error("Not logged in");
        }
    }
}