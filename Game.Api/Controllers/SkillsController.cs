using Game.Api.Models.Common;
using Game.Api.Models.Skills;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class SkillsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public SkillsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Skill> Skills(bool refreshCache = false)
        {
            var skills = _repositoryManager.Skills.AllSkills(refreshCache);
            return ApiResponse.Success(skills.To().Model<Skill>());
        }
    }
}
