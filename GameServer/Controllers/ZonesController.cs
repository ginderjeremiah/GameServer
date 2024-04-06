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
    public class ZonesController : BaseController
    {
        public ZonesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiResponse<List<Zone>> Zones()
        {
            return Success(Repositories.Zones.AllZones());
        }
    }
}
