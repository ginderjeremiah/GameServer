using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class StatisticsController(IPlayerProgressRepository playerProgress, SessionService sessionService) : ControllerBase
    {
        private readonly IPlayerProgressRepository _playerProgress = playerProgress;
        private readonly SessionService _sessionService = sessionService;

        [HttpGet("/api/[controller]")]
        public async Task<ApiEnumerableResponse<PlayerStatistic>> Statistics()
        {
            var stats = await _playerProgress.GetStatistics(_sessionService.SelectedPlayerId);
            return ApiResponse.Success(stats.To().Model<PlayerStatistic>());
        }

        [HttpGet]
        public ApiEnumerableResponse<StatisticType> StatisticTypes()
        {
            return ApiResponse.Success(Core.Progress.StatisticType.GetAll().To().Model<StatisticType>());
        }
    }
}
