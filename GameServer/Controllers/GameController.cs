using GameCore;
using GameCore.DataAccess;
using GameCore.Sessions;
using GameServer.Models.Common;
using GameServer.Models.Player;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/[action]")]
    public class GameController : BaseController
    {
        private readonly string _baseViewPath = "~/Views";
        private readonly SessionService _sessionService;

        public GameController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService)
        {
            _sessionService = sessionService;
        }

        [Route("/")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Default()
        {
            return Game();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Game()
        {
            return View($"{_baseViewPath}/Game.cshtml");
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Test()
        {
            return View($"{_baseViewPath}/Test.cshtml");
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult AdminTools()
        {
            return View($"{_baseViewPath}/AdminTools/AdminTools.cshtml");
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<ApiResponse<PlayerData>> Login([FromBody] LoginCredentials creds)
        {
            if (SessionAvailable)
                return Success(Session.GetPlayerData());

            var player = await Repositories.Players.GetPlayerByUserNameAsync(creds.Username);

            if (player is null)
                return Error<PlayerData>("Username not found");

            var passHash = creds.Password.Hash(player.Salt.ToString());

            if (passHash != player.PassHash)
                return Error<PlayerData>("Incorrect password");

            await _sessionService.CreateSession(player);

            return Success(Session.GetPlayerData());
        }

        [HttpGet]
        public ApiResponse LoginStatus()
        {
            return SessionAvailable ? Success() : Error("Not logged in");
        }
    }
}