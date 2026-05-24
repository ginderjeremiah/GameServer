using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Skills;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class SkillsController(ISkills skills) : ControllerBase
    {
        private readonly ISkills _skills = skills;

        [HttpGet("/api/[controller]")]
        public ApiEnumerableResponse<Skill> Skills(bool refreshCache = false)
        {
            return ApiResponse.Success(_skills.AllSkills(refreshCache).To().Model<Skill>());
        }
    }
}
