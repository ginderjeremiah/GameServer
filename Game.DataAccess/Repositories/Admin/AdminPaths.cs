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

    }
}
