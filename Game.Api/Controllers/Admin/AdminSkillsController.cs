using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting skills, their damage multipliers, and their effects.
    /// A thin HTTP adapter over <see cref="IAdminSkills"/>. The route prefix is shared across every
    /// admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminSkillsController(IAdminSkills adminSkills) : ControllerBase
    {
        private readonly IAdminSkills _adminSkills = adminSkills;

        [HttpPost]
        public ApiResponse AddEditSkills([FromBody] List<Change<Skill>> changes)
        {
            _adminSkills.SaveSkills(changes);
            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse SetSkillMultipliers([FromBody] AddEditAttributesData changeData)
        {
            return _adminSkills.SetMultipliers(changeData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Skill not found.");
        }

        [HttpPost]
        public ApiResponse SetSkillEffects([FromBody] SetSkillEffectsData changeData)
        {
            return _adminSkills.SetEffects(changeData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Skill not found.");
        }
    }
}
