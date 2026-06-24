using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for paths and their skill contributions. Reuses the cached entity
    /// lookups for existence/diff and builds fresh, navigation-free entities for every write. The identity
    /// save is retire-only (no hard delete); the contributions setter reconciles a full desired set.
    /// </summary>
    internal class AdminPaths(
        IProficiencyEntityCache proficiencies,
        ISkillEntityCache skills,
        IEntityStore entityStore) : IAdminPaths
    {
        private readonly IProficiencyEntityCache _proficiencies = proficiencies;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SavePaths(IReadOnlyList<Change<Contracts.Path>> changes)
        {
            // Authoring guard: retiring a path freezes it for XP accrual, so its tiers can never max. If one of
            // those tiers is authored as a prerequisite of a live (non-retired-path) gateway, that gateway could
            // then never open — a permanent soft-lock of live content. Reject it at save time; the runtime
            // suppression (#1203) deliberately doesn't paper over this authoring hazard (see docs/backend.md →
            // Retiring reference data). Reinstating a path only unblocks, so it is never guarded.
            if (FindRetiredPathGatingLiveGateway(changes) is { } rejection)
            {
                return rejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Path
                {
                    Name = item.Name,
                    Description = item.Description,
                    FalloffBase = item.FalloffBase,
                }),
                edit: item => _entityStore.Update(new Entities.Path
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    FalloffBase = item.FalloffBase,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "path",
                editExists: item => _proficiencies.LookupPath(item.Id) is not null);
        }

        public AdminSaveResult SetContributions(SetPathContributionsData data)
        {
            var path = _proficiencies.LookupPath(data.Id);
            if (path is null)
            {
                return AdminSaveResult.NotFound("Path");
            }

            foreach (var contribution in data.Contributions)
            {
                if (_skills.LookupSkill(contribution.SkillId) is null)
                {
                    return AdminSaveResult.Failure($"Skill {contribution.SkillId} does not exist.");
                }

                // The home tier names a tier of this path; it must resolve to a proficiency so the XP path
                // can route the contribution. (The reverse-index build depends on this holding.)
                if (_proficiencies.LookupProficiencyByTier(path.Id, contribution.HomeTier) is null)
                {
                    return AdminSaveResult.Failure(
                        $"Home tier {contribution.HomeTier} is not a tier of path '{path.Name}'.");
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: path.SkillContributions,
                desired: data.Contributions,
                existingKey: c => c.SkillId,
                desiredKey: c => c.SkillId,
                delete: c => _entityStore.Delete(new Entities.SkillPathContribution
                {
                    SkillId = c.SkillId,
                    PathId = path.Id,
                }),
                insert: c => _entityStore.Insert(ToContributionEntity(path.Id, c)),
                resourceName: "path contribution",
                update: c => _entityStore.Update(ToContributionEntity(path.Id, c)));
        }

        private static Entities.SkillPathContribution ToContributionEntity(int pathId, Contracts.SkillPathContribution contribution)
        {
            return new Entities.SkillPathContribution
            {
                SkillId = contribution.SkillId,
                PathId = pathId,
                HomeTier = contribution.HomeTier,
                Weight = contribution.Weight,
            };
        }

        /// <summary>
        /// Detects whether this save newly retires a path one of whose tiers is a prerequisite of a live
        /// (non-retired-path) gateway — a permanent soft-lock, since the frozen track can never max the
        /// prerequisite. Returns the user-facing rejection for the first such case, or null when the save is
        /// safe. Only the retirement transition matters: a path already retired before this save is left as-is
        /// (its dependents were already soft-locked, not by this edit), and reinstating is never a hazard.
        /// </summary>
        private AdminSaveResult? FindRetiredPathGatingLiveGateway(IReadOnlyList<Change<Contracts.Path>> changes)
        {
            // A malformed batch naming the same key more than once is rejected by the processor's shared
            // duplicate guard; skip this check (its per-id map can't represent a duplicated key) and let that
            // rejection stand rather than throwing here.
            if (ChangeSetProcessor.HasDuplicateKey(changes, change => change.ChangeType != EChangeType.Add, item => item.Id))
            {
                return null;
            }

            // Post-save retirement for each path this batch edits (a path outside the batch keeps its cached
            // state). Built once so the per-gateway checks are pure lookups.
            var editedRetirement = changes
                .Where(change => change.ChangeType == EChangeType.Edit)
                .ToDictionary(change => change.Item.Id, change => change.Item.RetiredAt is not null);

            // Only paths this save newly retires (live → retired) can introduce a soft-lock; an unrelated edit
            // that keeps a path retired is not blamed.
            var newlyRetiredPathIds = editedRetirement
                .Where(retirement => retirement.Value && _proficiencies.LookupPath(retirement.Key) is { RetiredAt: null })
                .Select(retirement => retirement.Key)
                .ToHashSet();

            if (newlyRetiredPathIds.Count == 0)
            {
                return null;
            }

            bool PathStaysLive(int pathId)
            {
                return editedRetirement.TryGetValue(pathId, out var retired)
                    ? !retired
                    : _proficiencies.LookupPath(pathId) is { RetiredAt: null };
            }

            // A gateway soft-locks when one of its prerequisites is a tier of a path this save newly retires
            // while the gateway's own path stays live: the frozen track never accrues, so the prerequisite never
            // maxes and the gateway never opens.
            foreach (var gateway in _proficiencies.AllProficiencyEntities())
            {
                if (!PathStaysLive(gateway.PathId))
                {
                    continue;
                }

                foreach (var prerequisite in gateway.Prerequisites)
                {
                    var prerequisiteProficiency = _proficiencies.LookupProficiency(prerequisite.PrerequisiteProficiencyId);
                    if (prerequisiteProficiency is not null && newlyRetiredPathIds.Contains(prerequisiteProficiency.PathId))
                    {
                        return AdminSaveResult.Failure(SoftLockRejection(prerequisiteProficiency, gateway));
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The rejection message for a retire that would soft-lock <paramref name="gateway"/>, naming the path
        /// being retired, the prerequisite tier on it, and the live gateway that could then never open.
        /// </summary>
        private string SoftLockRejection(Entities.Proficiency prerequisite, Entities.Proficiency gateway)
        {
            var pathName = _proficiencies.LookupPath(prerequisite.PathId)?.Name ?? $"path {prerequisite.PathId}";
            return $"Retiring path '{pathName}' would soft-lock live proficiency '{gateway.Name}', whose "
                + $"prerequisite '{prerequisite.Name}' is a tier of that path and could then never be maxed. "
                + "Retire the dependent proficiency's path first, or remove the prerequisite.";
        }
    }
}
