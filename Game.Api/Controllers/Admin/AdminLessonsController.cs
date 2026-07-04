using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting tutorial lessons and their step list (#1591, spike #1392). A thin
    /// HTTP adapter over <see cref="IAdminLessons"/>. The route prefix is shared across every admin controller so
    /// the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminLessonsController(IAdminLessons adminLessons) : ControllerBase
    {
        private readonly IAdminLessons _adminLessons = adminLessons;

        [HttpPost]
        public ApiResponse AddEditLessons([FromBody] List<Change<Lesson>> changes)
        {
            return _adminLessons.SaveLessons(changes);
        }

        [HttpPost]
        public ApiResponse SetLessonSteps([FromBody] SetLessonStepsData changeData)
        {
            return _adminLessons.SetSteps(changeData);
        }
    }
}
