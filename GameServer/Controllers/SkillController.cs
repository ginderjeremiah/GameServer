using DataAccess;
using DataAccess.Models.Skills;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class SkillController : BaseController
    {
        public SkillController(IRepositoryManager repositoryManager, ICacheManager cacheManager, IApiLogger logger)
            : base(repositoryManager, cacheManager, logger) { }

        [HttpGet]
        public ApiResponse<List<Skill>> Skills()
        {
            return Success(Caches.SkillCache.AllSkills());
        }
    }
}
