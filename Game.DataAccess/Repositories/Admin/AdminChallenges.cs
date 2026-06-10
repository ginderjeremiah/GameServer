using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for challenges. Builds fresh, navigation-free entities so an
    /// edit emits a single-row UPDATE without dragging in the type-derived statistic/entity graph.
    /// The contract's <c>StatisticType</c>/<c>EntityType</c> are read-only projections of the type
    /// and are ignored here — the type id is the only persisted classification.
    /// </summary>
    internal class AdminChallenges(IEntityStore entityStore) : IAdminChallenges
    {
        private readonly IEntityStore _entityStore = entityStore;

        public void SaveChallenges(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Challenge
                {
                    Name = item.Name,
                    Description = item.Description,
                    ChallengeTypeId = (int)item.ChallengeTypeId,
                    TargetEntityId = item.TargetEntityId,
                    ProgressGoal = item.ProgressGoal,
                    RewardItemId = item.RewardItemId,
                    RewardItemModId = item.RewardItemModId,
                    RewardSkillId = item.RewardSkillId,
                }),
                edit: item => _entityStore.Update(new Entities.Challenge
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    ChallengeTypeId = (int)item.ChallengeTypeId,
                    TargetEntityId = item.TargetEntityId,
                    ProgressGoal = item.ProgressGoal,
                    RewardItemId = item.RewardItemId,
                    RewardItemModId = item.RewardItemModId,
                    RewardSkillId = item.RewardSkillId,
                    RetiredAt = item.RetiredAt,
                }));
        }
    }
}
