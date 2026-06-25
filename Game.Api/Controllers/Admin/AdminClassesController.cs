using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting classes and their related collections (starter skills,
    /// starter equipment, attribute distributions). A thin HTTP adapter over <see cref="IAdminClasses"/> —
    /// the Content Authoring persistence shape (EF entities) is an implementation detail of the data tier and
    /// never surfaces here. The route prefix is shared across every admin controller so the existing
    /// <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminClassesController(IAdminClasses adminClasses) : ControllerBase
    {
        private readonly IAdminClasses _adminClasses = adminClasses;

        [HttpPost]
        public ApiResponse AddEditClasses([FromBody] List<Change<Class>> changes)
        {
            return _adminClasses.SaveClasses(changes);
        }

        [HttpPost]
        public ApiResponse SetClassStarterSkills([FromBody] SetClassStarterSkillsData starterSkillsData)
        {
            return _adminClasses.SetStarterSkills(starterSkillsData);
        }

        [HttpPost]
        public ApiResponse SetClassStarterEquipment([FromBody] SetClassStarterEquipmentData starterEquipmentData)
        {
            return _adminClasses.SetStarterEquipment(starterEquipmentData);
        }

        [HttpPost]
        public ApiResponse SetClassAttributeDistributions([FromBody] SetClassAttributeDistributionsData distributionsData)
        {
            return _adminClasses.SetAttributeDistributions(distributionsData);
        }
    }
}
