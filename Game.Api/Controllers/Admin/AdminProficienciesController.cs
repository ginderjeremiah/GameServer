using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting proficiencies and their related collections (per-level
    /// bonuses and per-level reward skills). A thin HTTP adapter over
    /// <see cref="IAdminProficiencies"/>. Skill contributions belong to the path — see
    /// <see cref="AdminPathsController"/>. The route prefix is shared across every admin controller so the
    /// existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminProficienciesController(IAdminProficiencies adminProficiencies) : ControllerBase
    {
        private readonly IAdminProficiencies _adminProficiencies = adminProficiencies;

        [HttpPost]
        public ApiResponse AddEditProficiencies([FromBody] List<Change<Proficiency>> changes)
        {
            return _adminProficiencies.SaveProficiencies(changes);
        }

        [HttpPost]
        public ApiResponse SetProficiencyModifiers([FromBody] SetProficiencyModifiersData changeData)
        {
            return _adminProficiencies.SetModifiers(changeData);
        }

        [HttpPost]
        public ApiResponse SetProficiencyRewards([FromBody] SetProficiencyRewardsData changeData)
        {
            return _adminProficiencies.SetRewards(changeData);
        }
    }
}
