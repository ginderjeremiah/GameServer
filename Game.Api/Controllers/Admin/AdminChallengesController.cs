using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting challenges. A challenge carries no child
    /// relationships, so it has a single whole-record Add/Edit/Delete endpoint. The route prefix
    /// is shared across every admin controller so the existing <c>/api/AdminTools/*</c> contract
    /// is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminChallengesController(IEntityStore entityStore) : ControllerBase
    {
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditChallenges([FromBody] List<Change<Challenge>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.Challenge
                {
                    Name = item.Name,
                    Description = item.Description,
                    ChallengeTypeId = (int)item.ChallengeTypeId,
                    TargetEntityId = item.TargetEntityId,
                    ProgressGoal = item.ProgressGoal,
                    RewardItemId = item.RewardItemId,
                    RewardItemModId = item.RewardItemModId,
                }),
                // Construct a fresh, navigation-free entity so EntityStore.Update emits a
                // single-row UPDATE without dragging in the type-derived statistic/entity graph.
                edit: item => _entityStore.Update(new Abstractions.Entities.Challenge
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    ChallengeTypeId = (int)item.ChallengeTypeId,
                    TargetEntityId = item.TargetEntityId,
                    ProgressGoal = item.ProgressGoal,
                    RewardItemId = item.RewardItemId,
                    RewardItemModId = item.RewardItemModId,
                }),
                delete: item => _entityStore.Delete(new Abstractions.Entities.Challenge
                {
                    Id = item.Id,
                    Name = "",
                    Description = "",
                }));

            return ApiResponse.Success();
        }
    }
}
