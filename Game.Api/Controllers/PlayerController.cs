using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly SessionService _sessionService;

        public PlayerController(SessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpGet("/api/[controller]")]
        public async Task<ApiResponse<PlayerData>> Player()
        {
            var player = await _sessionService.LoadPlayer();
            return ApiResponse.Success(PlayerData.FromPlayer(player));
        }
    }
}
