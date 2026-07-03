using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Core.Progress;
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
    internal class AdminChallenges(
        IChallenges challenges,
        IItemEntityCache items,
        IItemModEntityCache itemMods,
        IEnemyEntityCache enemies,
        IZoneEntityCache zones,
        ISkillEntityCache skills,
        IEntityStore entityStore) : IAdminChallenges
    {
        private readonly IChallenges _challenges = challenges;
        private readonly IItemEntityCache _items = items;
        private readonly IItemModEntityCache _itemMods = itemMods;
        private readonly IEnemyEntityCache _enemies = enemies;
        private readonly IZoneEntityCache _zones = zones;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveChallenges(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            // Authoring guards (rejected up front before anything is staged), mirroring the sibling admin
            // saves: ChallengeTypeId is an enum-seeded FK, and RewardItemId/RewardItemModId/TargetEntityId
            // would otherwise 500 on a bad FK (or an out-of-range enum) at commit.
            if (ReferenceFieldValidation.FindUndefinedEnum(changes, c => c.ChallengeTypeId, "challenge type") is { } typeRejection)
            {
                return typeRejection;
            }

            if (FindRewardViolation(changes) is { } rewardRejection)
            {
                return rewardRejection;
            }

            if (FindTargetViolation(changes) is { } targetRejection)
            {
                return targetRejection;
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
                    DesignerNotes = item.DesignerNotes,
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
                    DesignerNotes = item.DesignerNotes,
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
        /// Returns a rejection for the first added/edited challenge whose reward newly references a
        /// nonexistent item/item-mod, or newly references a retired one, or null when every reward is valid.
        /// Only a <em>changed</em> reward is checked — an edit's untouched reward already passed this guard
        /// when it was set (a reward resolves by id forever, so leaving an old reward in place as its target
        /// is later retired is expected, not an authoring mistake; see docs/backend.md). Deletes are skipped.
        /// </summary>
        private AdminSaveResult? FindRewardViolation(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var item = change.Item;
                var previous = change.ChangeType == EChangeType.Edit && _challenges.ValidateChallengeId(item.Id)
                    ? _challenges.GetChallenge(item.Id)
                    : null;

                if (item.RewardItemId is int rewardItemId && rewardItemId != previous?.RewardItemId)
                {
                    var rewardItem = _items.LookupItem(rewardItemId);
                    if (rewardItem is null)
                    {
                        return AdminSaveResult.Failure($"Reward item {rewardItemId} does not exist.");
                    }

                    if (rewardItem.RetiredAt is not null)
                    {
                        return AdminSaveResult.Failure($"Reward item '{rewardItem.Name}' is retired and cannot be newly assigned as a challenge reward.");
                    }
                }

                if (item.RewardItemModId is int rewardItemModId && rewardItemModId != previous?.RewardItemModId)
                {
                    var rewardItemMod = _itemMods.LookupItemMod(rewardItemModId);
                    if (rewardItemMod is null)
                    {
                        return AdminSaveResult.Failure($"Reward item mod {rewardItemModId} does not exist.");
                    }

                    if (rewardItemMod.RetiredAt is not null)
                    {
                        return AdminSaveResult.Failure($"Reward item mod '{rewardItemMod.Name}' is retired and cannot be newly assigned as a challenge reward.");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a rejection for the first added/edited challenge whose <c>TargetEntityId</c> is invalid for
        /// its type, or null when every target is valid. The dimension a target must resolve against is
        /// derived from the challenge type (mirrors the content-graph lint's <c>CheckChallenges</c>): an
        /// <see cref="EEntityType.Enemy"/>/<see cref="EEntityType.Zone"/>/
        /// <see cref="EEntityType.Skill"/> target must reference an existing record (retirement is tolerated —
        /// unlike a reward, a retired target can't fault the runtime; it only risks an eventually-uncompletable
        /// challenge, which the content-graph lint already flags post-hoc as a warning), and an
        /// <see cref="EEntityType.DamageType"/> target must be a defined <see cref="EDamageTypeKey"/> (a fixed
        /// intrinsic enum, not a DB reference table). <see cref="EEntityType.None"/> carries no dimension to
        /// validate. Deletes are skipped.
        /// </summary>
        private AdminSaveResult? FindTargetViolation(IReadOnlyList<Change<Contracts.Challenge>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var item = change.Item;

                // A KillsByDamageType challenge with no target can never track progress — the statistic writes
                // only per-damage-type-key rows, no global row (#1455).
                if (item.ChallengeTypeId == EChallengeType.KillsByDamageType && item.TargetEntityId is null)
                {
                    return AdminSaveResult.Failure("A 'Kills By Damage Type' challenge must target a damage-type key.");
                }

                if (item.TargetEntityId is not int targetId)
                {
                    continue;
                }

                var entityType = new ChallengeType(item.ChallengeTypeId).StatisticType?.EntityType ?? EEntityType.None;
                switch (entityType)
                {
                    case EEntityType.Enemy when _enemies.GetEnemy(targetId) is null:
                        return AdminSaveResult.Failure($"Target enemy {targetId} does not exist.");
                    case EEntityType.Zone when _zones.LookupZone(targetId) is null:
                        return AdminSaveResult.Failure($"Target zone {targetId} does not exist.");
                    case EEntityType.Skill when _skills.LookupSkill(targetId) is null:
                        return AdminSaveResult.Failure($"Target skill {targetId} does not exist.");
                    case EEntityType.DamageType when !Enum.IsDefined((EDamageTypeKey)targetId):
                        return AdminSaveResult.Failure($"{targetId} is not a valid damage-type key.");
                }
            }

            return null;
        }
    }
}
