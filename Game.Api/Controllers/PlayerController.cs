using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly SessionService _sessionService;
        private readonly PlayerService _playerService;

        public PlayerController(SessionService sessionService, PlayerService playerService)
        {
            _sessionService = sessionService;
            _playerService = playerService;
        }

        [HttpGet("/api/[controller]")]
        public async Task<ApiResponse<PlayerData>> Player()
        {
            var player = await _playerService.LoadPlayer(_sessionService.SelectedPlayerId)
                ?? throw new InvalidOperationException("Player data not loaded.");
            return ApiResponse.Success(PlayerData.FromPlayer(player));
        }
    }
}
