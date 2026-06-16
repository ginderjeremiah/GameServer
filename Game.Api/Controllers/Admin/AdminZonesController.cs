using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting zones and their enemy spawn assignments. A thin HTTP
    /// adapter over <see cref="IAdminZones"/>. The route prefix is shared across every admin controller
    /// so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminZonesController(IAdminZones adminZones) : ControllerBase
    {
        private readonly IAdminZones _adminZones = adminZones;

        [HttpPost]
        public ApiResponse AddEditZones([FromBody] List<Change<Zone>> changes)
        {
            return _adminZones.SaveZones(changes);
        }

        [HttpPost]
        public ApiResponse SetZoneEnemies([FromBody] SetZoneEnemiesData zoneEnemiesData)
        {
            return _adminZones.SetEnemies(zoneEnemiesData);
        }
    }
}
