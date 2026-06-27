using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;
using CorePath = Game.Core.Proficiencies.Path;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;
using Path = Game.Infrastructure.Entities.Path;
using SkillContribution = Game.Core.Proficiencies.SkillContribution;

namespace Game.DataAccess.Repositories
{
    internal class Proficiencies(ProficienciesCacheHolder holder) : IProficiencies, IProficiencyEntityCache
    {
        // Read the immutable snapshot once per logical operation (docs/backend.md → Reference-data snapshot
        // read-once idiom) so a build-then-swap between reads can't mix an old and a new snapshot in one call.
        private ProficiencySnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Proficiency> AllProficiencies()
        {
            return [.. Snapshot.Entities.Select(ProficiencyMapper.ToContract)];
        }

        public List<Contracts.Path> AllPaths()
        {
            return [.. Snapshot.Paths.Select(PathMapper.ToContract)];
        }

        public Proficiency? LookupProficiency(int proficiencyId)
        {
            return Snapshot.Entities.Lookup(proficiencyId);
        }

        public Path? LookupPath(int pathId)
        {
            return Snapshot.Paths.Lookup(pathId);
        }

        public Proficiency? LookupProficiencyByTier(int pathId, int ordinal)
        {
            return Snapshot.Entities.FirstOrDefault(p => p.PathId == pathId && p.PathOrdinal == ordinal);
        }

        public IReadOnlyList<Proficiency> AllProficiencyEntities()
        {
            return Snapshot.Entities;
        }

        public CoreProficiency GetProficiency(int proficiencyId)
        {
            // Returns the snapshot's shared, pre-materialized immutable instance rather than re-mapping.
            return Snapshot.CoreProficiencies.GetById(proficiencyId, "proficiency");
        }

        public CorePath GetPath(int pathId)
        {
            return Snapshot.CorePaths.GetById(pathId, "path");
        }

        public IReadOnlyList<SkillContribution> ContributionsForSkill(int skillId)
        {
            return Snapshot.ContributionsBySkill.TryGetValue(skillId, out var contributions)
                ? contributions
                : [];
        }
    }
}
