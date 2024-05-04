using DataAccess;
using GameCore.Logging.Interfaces;
using GameServer.Models.Common;
using GameServer.Models.Skills;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class SkillsController : BaseController
    {
        public SkillsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Skill> Skills()
        {
            return Success(Repositories.Skills.AllSkills().Select(skill => new Skill(skill)));
        }
    }
}
