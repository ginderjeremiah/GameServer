using GameCore;
using GameCore.DataAccess;
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
        public ApiListResponse<Zone> Zones()
        {
            var zones = Repositories.Zones.AllZones();
            return Success(zones.Select(z => new Zone(z)));
        }
    }
}
