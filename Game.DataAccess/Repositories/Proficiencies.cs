using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using Path = Game.Infrastructure.Entities.Path;
using SkillContribution = Game.Core.Proficiencies.SkillContribution;

namespace Game.DataAccess.Repositories
{
    internal class Proficiencies(ProficienciesCacheHolder holder) : IProficiencies, IProficiencyEntityCache
    {
        private IReadOnlyList<Proficiency> Entities => holder.Current.Entities;
        private IReadOnlyList<Path> PathEntities => holder.Current.Paths;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => holder.Current;

        public List<Contracts.Proficiency> AllProficiencies()
        {
            return [.. Entities.Select(ProficiencyMapper.ToContract)];
        }

        public List<Contracts.Path> AllPaths()
        {
            return [.. PathEntities.Select(PathMapper.ToContract)];
        }

        public Proficiency? LookupProficiency(int proficiencyId)
        {
            return Entities.Lookup(proficiencyId);
        }

        public Path? LookupPath(int pathId)
        {
            return PathEntities.Lookup(pathId);
        }

        public Proficiency? LookupProficiencyByTier(int pathId, int ordinal)
        {
            return Entities.FirstOrDefault(p => p.PathId == pathId && p.PathOrdinal == ordinal);
        }

        public CoreProficiency GetProficiency(int proficiencyId)
        {
            // Returns the snapshot's shared, pre-materialized immutable instance rather than re-mapping.
            return holder.Current.CoreProficiencies.GetById(proficiencyId, "proficiency");
        }

        public IReadOnlyList<SkillContribution> ContributionsForSkill(int skillId)
        {
            return holder.Current.ContributionsBySkill.TryGetValue(skillId, out var contributions)
                ? contributions
                : [];
        }
    }
}
