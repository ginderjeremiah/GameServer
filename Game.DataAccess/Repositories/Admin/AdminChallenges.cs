using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
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
    internal class AdminChallenges(IChallenges challenges, IEntityStore entityStore) : IAdminChallenges
    {
        private readonly IChallenges _challenges = challenges;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveChallenges(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            // An edit must target an existing challenge; a missing id is a not-found rejection (matching the
            // relationship setters), not an EF 0-row update that throws. Challenges are zero-based-id
            // reference data, so a valid id is an in-range index. Validate the whole batch up front so the
            // commit filter doesn't persist the rest of the batch alongside an invalid edit.
            var challengeCount = _challenges.All().Count;
            if (changes.Any(c => c.ChangeType == EChangeType.Edit
                && (c.Item.Id < 0 || c.Item.Id >= challengeCount)))
            {
                return AdminSaveResult.NotFound("Challenge");
            }

            return ChangeSetProcessor.Apply(changes,
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
