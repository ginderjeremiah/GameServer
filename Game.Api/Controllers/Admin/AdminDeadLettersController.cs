using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Ops endpoints for the write-behind dead-letter queues: a read-only inspection surface
    /// (depth + a classified, head-first peek) and a guarded replay for each of the player write-behind
    /// queue (#794) and the socket command queue (#1542). A thin HTTP adapter over
    /// <see cref="IPlayerUpdateDeadLetters"/> and <see cref="SocketCommandDeadLetters"/> — the queue
    /// mechanics live in the data tier / socket layer respectively. Shares the <c>/api/AdminTools/*</c> route
    /// prefix and requires the Admin role. Unlike the reference-data admin controllers it carries no
    /// <see cref="ReloadReferenceCachesAttribute"/>: it touches no reference cache.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    public class AdminDeadLettersController(IPlayerUpdateDeadLetters deadLetters, SocketCommandDeadLetters socketCommandDeadLetters) : ControllerBase
    {
        private const int DefaultInspectLimit = 50;
        private const int MaxInspectLimit = 500;

        private readonly IPlayerUpdateDeadLetters _deadLetters = deadLetters;
        private readonly SocketCommandDeadLetters _socketCommandDeadLetters = socketCommandDeadLetters;

        [HttpGet]
        public async Task<ApiResponse<DeadLetterInspection>> GetPlayerUpdateDeadLetters(int max = DefaultInspectLimit)
        {
            max = Math.Clamp(max, 0, MaxInspectLimit);
            return ApiResponse.Success(await _deadLetters.InspectAsync(max));
        }

        [HttpPost]
        public async Task<ApiResponse<DeadLetterReplayResult>> ReplayPlayerUpdateDeadLetters([FromBody] ReplayDeadLettersData data)
        {
            var result = data.All
                ? await _deadLetters.ReplayAllAsync()
                : await _deadLetters.ReplaySelectedAsync(data.Payloads ?? []);

            return ApiResponse.Success(result);
        }

        [HttpGet]
        public async Task<ApiResponse<DeadLetterInspection>> GetSocketCommandDeadLetters(int max = DefaultInspectLimit)
        {
            max = Math.Clamp(max, 0, MaxInspectLimit);
            return ApiResponse.Success(await _socketCommandDeadLetters.InspectAsync(max));
        }

        [HttpPost]
        public async Task<ApiResponse<DeadLetterReplayResult>> ReplaySocketCommandDeadLetters([FromBody] ReplayDeadLettersData data)
        {
            var result = data.All
                ? await _socketCommandDeadLetters.ReplayAllAsync()
                : await _socketCommandDeadLetters.ReplaySelectedAsync(data.Payloads ?? []);

            return ApiResponse.Success(result);
        }
    }
}
