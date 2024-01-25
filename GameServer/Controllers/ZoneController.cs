using DataAccess;
using DataAccess.Models.Zones;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ZoneController : BaseController
    {
        public ZoneController(IRepositoryManager repositoryManager, ICacheManager cacheManager, IApiLogger logger)
            : base(repositoryManager, cacheManager, logger) { }

        [HttpGet]
        public ApiResponse<List<Zone>> Zones()
        {
            return Success(Caches.ZoneCache.AllZones());
        }
    }
}
