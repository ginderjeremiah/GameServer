using Game.Abstractions.DataAccess;
using Game.Api.Models.Challenges;
using Game.Api.Models.Common;
using Game.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ChallengesController(IChallenges challenges, IPlayerChallenges playerChallenges, SessionService sessionService) : ControllerBase
    {
        private readonly IChallenges _challenges = challenges;
        private readonly IPlayerChallenges _playerChallenges = playerChallenges;
        private readonly SessionService _sessionService = sessionService;

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Challenge> Challenges()
        {
            return ApiResponse.Success(_challenges.All().To().Model<Challenge>());
        }

        [HttpGet]
        public async Task<ApiEnumerableResponse<PlayerChallenge>> Player()
        {
            var player = await _sessionService.LoadPlayer();
            var progress = await _playerChallenges.GetPlayerChallenges(player.Id);
            return ApiResponse.Success(progress.To().Model<PlayerChallenge>());
        }
    }
}
