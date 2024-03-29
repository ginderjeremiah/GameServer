﻿using DataAccess;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Request;
using GameServer.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/[action]")]
    public class GameController : BaseController
    {
        private readonly string _baseViewPath = "~/Views";

        public GameController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

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

        [SessionAuthorize]
        [HttpGet]
        public ApiResponse<string> LoginStatus()
        {
            return Success("Logged in");
        }

        [SessionAuthorize(AllowAll = true)]
        [HttpPost]
        public ApiResponse<LoginResponse> Login([FromBody] LoginCredentials creds)
        {
            if (Session != null)
                return Success(new LoginResponse { CurrentZone = Session.CurrentZone, PlayerData = Session.PlayerData });

            var player = Repositories.Players.GetPlayerByUserName(creds.Username);

            if (player is null)
                return Error<LoginResponse>("Username not found");

            var passHash = creds.Password.Hash(player.Salt.ToString());

            if (passHash != player.PassHash)
                return Error<LoginResponse>("Incorrect password");

            var sessionData = Repositories.SessionStore.GetNewSessionData(player.PlayerId);

            var session = new Session(sessionData, Repositories);
            var token = session.GetNewToken();
            Response.Cookies.Append("sessionToken", token, DefaultCookieOptions);

            return Success(new LoginResponse()
            {
                CurrentZone = session.CurrentZone,
                PlayerData = session.PlayerData
            });
        }
    }
}