using DataAccess;
using GameCore.Logging.Interfaces;
using GameServer.Models.Common;
using GameServer.Models.Zones;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ZonesController : BaseController
    {
        public ZonesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Zone> Zones()
        {
            return Success(Repositories.Zones.AllZones().Select(z => new Zone(z)));
        }
    }
}
