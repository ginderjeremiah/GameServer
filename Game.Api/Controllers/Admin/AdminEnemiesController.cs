using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting enemies and their related collections (attribute
    /// distributions, skill pools, and zone spawns). A thin HTTP adapter over <see cref="IAdminEnemies"/>
    /// — the Content Authoring persistence shape (EF entities) is an implementation detail of the data
    /// tier and never surfaces here. The route prefix is shared across every admin controller so the
    /// existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ReloadReferenceCaches]
    public class AdminEnemiesController(IAdminEnemies adminEnemies) : ControllerBase
    {
        private readonly IAdminEnemies _adminEnemies = adminEnemies;

        [HttpPost]
        public ApiResponse AddEditEnemies([FromBody] List<Change<Enemy>> changes)
        {
            return _adminEnemies.SaveEnemies(changes)
                ? ApiResponse.Success()
                : ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemyAttributeDistributions([FromBody] SetEnemyAttributeDistributions distributionsData)
        {
            return _adminEnemies.SetAttributeDistributions(distributionsData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemySkills([FromBody] SetEnemySkillsData enemySkillsData)
        {
            return _adminEnemies.SetSkills(enemySkillsData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemySpawns([FromBody] SetEnemySpawnsData spawnsData)
        {
            return _adminEnemies.SetSpawns(spawnsData)
                ? ApiResponse.Success()
                : ApiResponse.Error("Enemy not found.");
        }
    }
}
