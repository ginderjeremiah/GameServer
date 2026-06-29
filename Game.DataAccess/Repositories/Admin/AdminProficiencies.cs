using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.DataAccess.Repositories.Caching;
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
            // A proficiency is a tier of a path; the path it names must exist (the FK would also reject it,
            // but a named check fails the whole set cleanly before anything is staged).
            if (FindPathViolation(changes) is { } pathRejection)
            {
                return pathRejection;
            }

            // (PathId, PathOrdinal) is unique per path (a path's tiers form a contiguous frontier). The DB
            // unique index is the backstop, but a raw violation surfaces as a 500; validate the full prospective
            // per-path tier layout (not a per-change cache probe, which would false-reject a valid reorder/swap)
            // and fail cleanly before anything is staged.
            if (FindTierOrdinalCollision(changes) is { } collisionRejection)
            {
                return collisionRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Proficiency
                {
                    Name = item.Name,
                    Description = item.Description,
                    IconPath = item.IconPath,
                    Word = item.Word,
                    Pronunciation = item.Pronunciation,
                    Translation = item.Translation,
                    PathId = item.PathId,
                    PathOrdinal = item.PathOrdinal,
                    MaxLevel = item.MaxLevel,
                    BaseXp = item.BaseXp,
                    XpGrowth = item.XpGrowth,
                }),
                edit: item => _entityStore.Update(new Entities.Proficiency
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    IconPath = item.IconPath,
                    Word = item.Word,
                    Pronunciation = item.Pronunciation,
                    Translation = item.Translation,
                    PathId = item.PathId,
                    PathOrdinal = item.PathOrdinal,
                    MaxLevel = item.MaxLevel,
                    BaseXp = item.BaseXp,
                    XpGrowth = item.XpGrowth,
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
            // interpreted in #1116), so a level outside 0..MaxLevel would author a payout that never fires.
            // Level 0 is allowed for modifiers: the cumulative bonus rule (Proficiency.ModifiersForLevel uses
            // l.Level <= level) honors a payout authored at the just-opened state.
            if (FindLevelOutOfRange(proficiency, data.Modifiers.Select(m => m.Level), "level modifier", minLevel: 0) is { } rejection)
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

            // A milestone reward only pays out at a reachable, crossable level, so reject one authored outside
            // 1..MaxLevel before the anti-tamper skill check. Unlike modifiers, level 0 is not allowed: a reward
            // is granted by crossing a milestone (Proficiency.RewardSkillsCrossed uses l.Level > fromLevel, and
            // fromLevel is never below 0), so a level-0 reward could never fire.
            if (FindLevelOutOfRange(proficiency, data.Rewards.Select(r => r.Level), "level reward", minLevel: 1) is { } levelRejection)
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

            // Reject a prerequisite that would cycle (A needs B needs A), which would soft-lock both nodes.
            // Build the prospective graph — every other proficiency's existing edges plus this one's desired
            // set — and run full cycle detection before anything commits.
            if (FindPrerequisiteCycle(proficiency.Id, data.PrerequisiteIds) is { } cycleRejection)
            {
                return cycleRejection;
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

        /// <summary>Returns a rejection if any added/edited proficiency names a path that does not exist or a
        /// negative ordinal, else null.</summary>
        private AdminSaveResult? FindPathViolation(IReadOnlyList<Change<Contracts.Proficiency>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                if (_proficiencies.LookupPath(change.Item.PathId) is null)
                {
                    return AdminSaveResult.Failure($"Path {change.Item.PathId} does not exist.");
                }

                if (change.Item.PathOrdinal < 0)
                {
                    return AdminSaveResult.Failure("A proficiency's path ordinal (tier) cannot be negative.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if the prospective per-path tier layout would place two proficiencies at
        /// the same <c>PathOrdinal</c> within one path, else null. The prospective set is the cached proficiencies
        /// with each Edit replacing its record (by Id), each Add appended, and each Delete removed — so a clean
        /// reorder/swap of existing ordinals is accepted while a true collision is named before anything stages.
        /// The DB unique index remains the backstop.</summary>
        private AdminSaveResult? FindTierOrdinalCollision(IReadOnlyList<Change<Contracts.Proficiency>> changes)
        {
            // Start from the current cached layout keyed by Id, then fold the batch in.
            var prospective = _proficiencies.AllProficiencyEntities()
                .ToDictionary(p => p.Id, p => (p.PathId, p.PathOrdinal));

            foreach (var change in changes)
            {
                switch (change.ChangeType)
                {
                    case EChangeType.Delete:
                        prospective.Remove(change.Item.Id);
                        break;
                    case EChangeType.Edit:
                        prospective[change.Item.Id] = (change.Item.PathId, change.Item.PathOrdinal);
                        break;
                    case EChangeType.Add:
                        // Adds carry an unassigned Id, so they can't key the dictionary; collect them separately.
                        break;
                }
            }

            var tiers = prospective.Values
                .Concat(changes.Where(c => c.ChangeType == EChangeType.Add).Select(c => (c.Item.PathId, c.Item.PathOrdinal)));

            var seen = new HashSet<(int PathId, int PathOrdinal)>();
            foreach (var tier in tiers)
            {
                if (!seen.Add(tier))
                {
                    var pathName = _proficiencies.LookupPath(tier.PathId)?.Name;
                    var pathLabel = pathName is null ? $"{tier.PathId}" : $"'{pathName}'";
                    return AdminSaveResult.Failure($"Path {pathLabel} has two tiers at ordinal {tier.PathOrdinal}.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if any authored level falls outside <c><paramref name="minLevel"/>..MaxLevel</c>,
        /// else null. A payout level must be reachable, and levels past the cap never fire. <paramref name="minLevel"/>
        /// is 0 for modifiers (a payout authored at the just-opened state is honored cumulatively) and 1 for rewards
        /// (a reward fires only by crossing a milestone, which a level-0 reward never does).</summary>
        private static AdminSaveResult? FindLevelOutOfRange(Entities.Proficiency proficiency, IEnumerable<int> levels, string role, int minLevel)
        {
            foreach (var level in levels)
            {
                if (level < minLevel || level > proficiency.MaxLevel)
                {
                    return AdminSaveResult.Failure(
                        $"Proficiency {role} level {level} is out of range (must be between {minLevel} and the cap of {proficiency.MaxLevel}).");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if setting <paramref name="proficiencyId"/>'s prerequisites to
        /// <paramref name="desiredPrerequisiteIds"/> would introduce a cycle in the prerequisite graph, else
        /// null. The prospective graph keeps every other proficiency's existing edges and overrides only this
        /// node's.</summary>
        private AdminSaveResult? FindPrerequisiteCycle(int proficiencyId, IReadOnlyList<int> desiredPrerequisiteIds)
        {
            var graph = _proficiencies.AllProficiencyEntities().ToDictionary(
                p => p.Id,
                p => (IReadOnlyList<int>)p.Prerequisites.Select(pr => pr.PrerequisiteProficiencyId).ToList());
            graph[proficiencyId] = desiredPrerequisiteIds;

            if (ProficiencyPrerequisiteGraph.TryFindCycle(graph, out var cycle))
            {
                return AdminSaveResult.Failure(
                    $"These prerequisites would create a cycle: {string.Join(" -> ", cycle)}.");
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
