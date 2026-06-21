using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
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
    internal class AdminChallenges(IChallenges challenges, ISkillEntityCache skills, IEntityStore entityStore) : IAdminChallenges
    {
        private readonly IChallenges _challenges = challenges;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveChallenges(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            // Authoring guard (anti-tamper): a skill set as a challenge reward must declare itself
            // Player-acquirable. The flag is the declared intent; this reference is the reality, so the
            // save bridges them — rejected up front before anything is staged (a tampered admin client
            // can't bypass the frontend's filtered picker).
            if (FindRewardSkillFlagViolation(changes) is { } rejection)
            {
                return rejection;
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
                }),
                key: item => item.Id,
                resourceName: "challenge",
                // An edit must target an existing challenge; a missing id is a not-found rejection (matching the
                // relationship setters), not an EF 0-row update that throws. Challenges are zero-based-id
                // reference data, so existence is the shared in-range index check.
                editExists: item => _challenges.ValidateChallengeId(item.Id));
        }

        /// <summary>
        /// Returns a rejection for the first added/edited challenge whose <c>RewardSkillId</c> targets a skill
        /// that is not <see cref="ESkillAcquisition.Player"/>-flagged (or does not exist), or null when every
        /// reward skill is valid. Deletes carry no reward intent and are skipped.
        /// </summary>
        private AdminSaveResult? FindRewardSkillFlagViolation(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete || change.Item.RewardSkillId is not { } skillId)
                {
                    continue;
                }

                var skill = _skills.LookupSkill(skillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Reward skill {skillId} does not exist.");
                }

                if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Player))
                {
                    return AdminSaveResult.Failure(
                        $"Skill '{skill.Name}' is not flagged as Player-acquirable and cannot be a challenge reward.");
                }
            }

            return null;
        }
    }
}
