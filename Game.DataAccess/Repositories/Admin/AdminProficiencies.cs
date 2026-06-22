using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for proficiencies and their related collections. Reuses the cached
    /// entity lookups for existence/diff and builds fresh, navigation-free entities for every write. The
    /// identity save is retire-only (no hard delete); the relationship setters reconcile a full desired set.
    /// </summary>
    internal class AdminProficiencies(
        IProficiencyEntityCache proficiencies,
        ISkillEntityCache skills,
        IEntityStore entityStore) : IAdminProficiencies
    {
        private readonly IProficiencyEntityCache _proficiencies = proficiencies;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveProficiencies(IReadOnlyList<Change<Contracts.Proficiency>> changes)
        {
            // Anti-tamper: a tree-seed skill is a permanent grant, so it must declare itself Player-acquirable
            // (the flag is intent; this reference is reality). Rejected up front before anything is staged.
            if (FindSeedSkillFlagViolation(changes) is { } rejection)
            {
                return rejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Proficiency
                {
                    Name = item.Name,
                    Description = item.Description,
                    IconPath = item.IconPath,
                    MaxLevel = item.MaxLevel,
                    BaseXp = item.BaseXp,
                    XpGrowth = item.XpGrowth,
                    StartsUnlocked = item.StartsUnlocked,
                    SeedSkillId = item.SeedSkillId,
                }),
                edit: item => _entityStore.Update(new Entities.Proficiency
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    IconPath = item.IconPath,
                    MaxLevel = item.MaxLevel,
                    BaseXp = item.BaseXp,
                    XpGrowth = item.XpGrowth,
                    StartsUnlocked = item.StartsUnlocked,
                    SeedSkillId = item.SeedSkillId,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "proficiency",
                editExists: item => _proficiencies.LookupProficiency(item.Id) is not null);
        }

        public AdminSaveResult SetModifiers(SetProficiencyModifiersData data)
        {
            var proficiency = _proficiencies.LookupProficiency(data.Id);
            if (proficiency is null)
            {
                return AdminSaveResult.NotFound("Proficiency");
            }

            // A per-level bonus only pays out at a level the proficiency can actually reach (the curve is
            // interpreted in #1116), so a level outside 1..MaxLevel would author a payout that never fires.
            if (FindLevelOutOfRange(proficiency, data.Modifiers.Select(m => m.Level), "level modifier") is { } rejection)
            {
                return rejection;
            }

            return ChildCollectionReconciler.Reconcile(
                existing: proficiency.LevelModifiers,
                desired: data.Modifiers,
                existingKey: m => (m.Level, m.AttributeId),
                desiredKey: m => (m.Level, (int)m.AttributeId),
                delete: m => _entityStore.Delete(new Entities.ProficiencyLevelModifier
                {
                    ProficiencyId = proficiency.Id,
                    Level = m.Level,
                    AttributeId = m.AttributeId,
                }),
                insert: m => _entityStore.Insert(ToModifierEntity(proficiency.Id, m)),
                resourceName: "proficiency level modifier",
                update: m => _entityStore.Update(ToModifierEntity(proficiency.Id, m)));
        }

        public AdminSaveResult SetRewards(SetProficiencyRewardsData data)
        {
            var proficiency = _proficiencies.LookupProficiency(data.Id);
            if (proficiency is null)
            {
                return AdminSaveResult.NotFound("Proficiency");
            }

            // A milestone reward only pays out at a reachable level (see SetModifiers), so reject one authored
            // outside 1..MaxLevel before the anti-tamper skill check.
            if (FindLevelOutOfRange(proficiency, data.Rewards.Select(r => r.Level), "level reward") is { } levelRejection)
            {
                return levelRejection;
            }

            // Anti-tamper: a milestone reward skill is a permanent grant, so it must be Player-acquirable.
            foreach (var reward in data.Rewards)
            {
                if (CheckPlayerSkill(reward.RewardSkillId, "milestone reward skill") is { } rejection)
                {
                    return rejection;
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: proficiency.LevelRewards,
                desired: data.Rewards,
                existingKey: r => r.Level,
                desiredKey: r => r.Level,
                delete: r => _entityStore.Delete(new Entities.ProficiencyLevelReward
                {
                    ProficiencyId = proficiency.Id,
                    Level = r.Level,
                }),
                insert: r => _entityStore.Insert(ToRewardEntity(proficiency.Id, r)),
                resourceName: "proficiency level reward",
                update: r => _entityStore.Update(ToRewardEntity(proficiency.Id, r)));
        }

        public AdminSaveResult SetPrerequisites(SetProficiencyPrerequisitesData data)
        {
            var proficiency = _proficiencies.LookupProficiency(data.Id);
            if (proficiency is null)
            {
                return AdminSaveResult.NotFound("Proficiency");
            }

            foreach (var prerequisiteId in data.PrerequisiteIds)
            {
                if (prerequisiteId == proficiency.Id)
                {
                    return AdminSaveResult.Failure("A proficiency cannot be its own prerequisite.");
                }

                if (_proficiencies.LookupProficiency(prerequisiteId) is null)
                {
                    return AdminSaveResult.Failure($"Prerequisite proficiency {prerequisiteId} does not exist.");
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: proficiency.Prerequisites,
                desired: data.PrerequisiteIds,
                existingKey: p => p.PrerequisiteProficiencyId,
                desiredKey: id => id,
                delete: p => _entityStore.Delete(new Entities.ProficiencyPrerequisite
                {
                    ProficiencyId = proficiency.Id,
                    PrerequisiteProficiencyId = p.PrerequisiteProficiencyId,
                }),
                insert: id => _entityStore.Insert(new Entities.ProficiencyPrerequisite
                {
                    ProficiencyId = proficiency.Id,
                    PrerequisiteProficiencyId = id,
                }),
                resourceName: "proficiency prerequisite");
        }

        public AdminSaveResult SetContributions(SetProficiencyContributionsData data)
        {
            var proficiency = _proficiencies.LookupProficiency(data.Id);
            if (proficiency is null)
            {
                return AdminSaveResult.NotFound("Proficiency");
            }

            foreach (var contribution in data.Contributions)
            {
                if (_skills.LookupSkill(contribution.SkillId) is null)
                {
                    return AdminSaveResult.Failure($"Skill {contribution.SkillId} does not exist.");
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: proficiency.SkillContributions,
                desired: data.Contributions,
                existingKey: c => c.SkillId,
                desiredKey: c => c.SkillId,
                delete: c => _entityStore.Delete(new Entities.SkillProficiency
                {
                    SkillId = c.SkillId,
                    ProficiencyId = proficiency.Id,
                }),
                insert: c => _entityStore.Insert(ToContributionEntity(proficiency.Id, c)),
                resourceName: "proficiency contribution",
                update: c => _entityStore.Update(ToContributionEntity(proficiency.Id, c)));
        }

        private static Entities.ProficiencyLevelModifier ToModifierEntity(int proficiencyId, Contracts.ProficiencyLevelModifier modifier)
        {
            return new Entities.ProficiencyLevelModifier
            {
                ProficiencyId = proficiencyId,
                Level = modifier.Level,
                AttributeId = (int)modifier.AttributeId,
                ModifierType = (int)modifier.ModifierTypeId,
                Amount = modifier.Amount,
            };
        }

        private static Entities.ProficiencyLevelReward ToRewardEntity(int proficiencyId, Contracts.ProficiencyLevelReward reward)
        {
            return new Entities.ProficiencyLevelReward
            {
                ProficiencyId = proficiencyId,
                Level = reward.Level,
                RewardSkillId = reward.RewardSkillId,
            };
        }

        private static Entities.SkillProficiency ToContributionEntity(int proficiencyId, Contracts.SkillProficiencyContribution contribution)
        {
            return new Entities.SkillProficiency
            {
                SkillId = contribution.SkillId,
                ProficiencyId = proficiencyId,
                Weight = contribution.Weight,
            };
        }

        /// <summary>Returns a rejection if any authored level falls outside <c>1..MaxLevel</c>, else null. A
        /// payout level must be reachable: level 0 is the untrained state and levels past the cap never
        /// fire.</summary>
        private static AdminSaveResult? FindLevelOutOfRange(Entities.Proficiency proficiency, IEnumerable<int> levels, string role)
        {
            foreach (var level in levels)
            {
                if (level < 1 || level > proficiency.MaxLevel)
                {
                    return AdminSaveResult.Failure(
                        $"Proficiency {role} level {level} is out of range (must be between 1 and the cap of {proficiency.MaxLevel}).");
                }
            }

            return null;
        }

        private AdminSaveResult? FindSeedSkillFlagViolation(IReadOnlyList<Change<Contracts.Proficiency>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete || change.Item.SeedSkillId is not { } skillId)
                {
                    continue;
                }

                if (CheckPlayerSkill(skillId, "seed skill") is { } rejection)
                {
                    return rejection;
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if the skill does not exist or is not Player-acquirable, else null.</summary>
        private AdminSaveResult? CheckPlayerSkill(int skillId, string role)
        {
            var skill = _skills.LookupSkill(skillId);
            if (skill is null)
            {
                return AdminSaveResult.Failure($"Skill {skillId} does not exist.");
            }

            if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Player))
            {
                return AdminSaveResult.Failure(
                    $"Skill '{skill.Name}' is not flagged as Player-acquirable and cannot be a proficiency {role}.");
            }

            return null;
        }
    }
}
