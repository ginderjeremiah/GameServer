using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

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
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminChallengesController(IEntityStore entityStore) : ControllerBase
    {
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditChallenges([FromBody] List<Change<Challenge>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.Challenge
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ChallengeTypeId = (int)change.Item.ChallengeTypeId,
                        TargetEntityId = change.Item.TargetEntityId,
                        ProgressGoal = change.Item.ProgressGoal,
                        RewardItemId = change.Item.RewardItemId,
                        RewardItemModId = change.Item.RewardItemModId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Construct a fresh, navigation-free entity so EntityStore.Update emits a
                    // single-row UPDATE without dragging in the type-derived statistic/entity graph.
                    _entityStore.Update(new Abstractions.Entities.Challenge
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ChallengeTypeId = (int)change.Item.ChallengeTypeId,
                        TargetEntityId = change.Item.TargetEntityId,
                        ProgressGoal = change.Item.ProgressGoal,
                        RewardItemId = change.Item.RewardItemId,
                        RewardItemModId = change.Item.RewardItemModId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Challenge
                    {
                        Id = change.Item.Id,
                        Name = "",
                        Description = "",
                    });
                }
            }

            return ApiResponse.Success();
        }
    }
}
