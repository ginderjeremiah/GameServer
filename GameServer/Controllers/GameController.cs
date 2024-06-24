using GameCore;
using GameCore.Sessions;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Player;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/[action]")]
    public class GameController : BaseController
    {
        private readonly string _baseViewPath = "~/Views";

        public GameController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

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

        [SessionAuthorize]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult AdminTools()
        {
            return View($"{_baseViewPath}/AdminTools/AdminTools.cshtml");
        }

        [SessionAuthorize(AllowAll = true)]
        [HttpPost]
        public async Task<ApiResponse<LoginData>> Login([FromBody] LoginCredentials creds)
        {
            if (SessionAvailable)
                return Success(new LoginData { CurrentZone = Session.CurrentZone, PlayerData = Session.GetPlayerData() });

            var player = await Repositories.Players.GetPlayerByUserNameAsync(creds.Username);

            if (player is null)
                return Error<LoginData>("Username not found");

            var passHash = creds.Password.Hash(player.Salt.ToString());

            if (passHash != player.PassHash)
                return Error<LoginData>("Incorrect password");

            var sessionData = await Repositories.SessionStore.GetNewSessionDataAsync(player.Id);

            var session = new Session(sessionData, Repositories);
            var token = session.GetNewToken();
            Response.Cookies.Append("sessionToken", token, DefaultCookieOptions);

            return Success(new LoginData()
            {
                CurrentZone = session.CurrentZone,
                PlayerData = session.GetPlayerData()
            });
        }

        [SessionAuthorize]
        [HttpGet]
        public ApiResponse LoginStatus()
        {
            return Success();
        }
    }
}