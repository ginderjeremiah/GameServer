using GameCore;
using GameCore.DataAccess;
using GameServer.Models.Common;
using GameServer.Models.Skills;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class SkillsController : BaseController
    {
        public SkillsController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<Skill> Skills()
        {
            var skills = Repositories.Skills.AllSkills();
            return Success(skills.Select(skill => new Skill(skill)));
        }
    }
}
