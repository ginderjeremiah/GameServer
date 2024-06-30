using GameCore;
using GameServer.Models.Common;
using GameServer.Models.Zones;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ZonesController : BaseController
    {
        public ZonesController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public async Task<ApiListResponse<Zone>> Zones()
        {
            var zones = await Repositories.Zones.AllZonesAsync();
            return Success(zones.Select(z => new Zone(z)));
        }
    }
}
