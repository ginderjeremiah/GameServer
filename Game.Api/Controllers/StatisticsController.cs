using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Statistics;
using Game.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class StatisticsController(IPlayerStatistics playerStatistics, SessionService sessionService) : ControllerBase
    {
        private readonly IPlayerStatistics _playerStatistics = playerStatistics;
        private readonly SessionService _sessionService = sessionService;

        [HttpGet("/api/[controller]")]
        public async Task<ApiEnumerableResponse<PlayerStatistic>> Statistics()
        {
            var player = await _sessionService.LoadPlayer();
            var stats = await _playerStatistics.GetPlayerStatistics(player.Id);
            return ApiResponse.Success(stats.To().Model<PlayerStatistic>());
        }
    }
}
