using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ChallengesController(IPlayerProgressRepository playerProgress, SessionService sessionService) : ControllerBase
    {
        private readonly IPlayerProgressRepository _playerProgress = playerProgress;
        private readonly SessionService _sessionService = sessionService;

        [HttpGet]
        public async Task<ApiEnumerableResponse<PlayerChallenge>> Player()
        {
            var player = await _sessionService.LoadPlayer();
            var progress = await _playerProgress.GetChallenges(player.Id);
            return ApiResponse.Success(progress.To().Model<PlayerChallenge>());
        }
    }
}
