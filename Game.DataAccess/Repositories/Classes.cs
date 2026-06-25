using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Contracts = Game.Abstractions.Contracts;
using CoreClass = Game.Core.Classes.Class;
using EntityClass = Game.Infrastructure.Entities.Class;

namespace Game.DataAccess.Repositories
{
    internal class Classes(ClassesCacheHolder holder) : IClasses, IClassEntityCache
    {
        private ClassSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Class> All()
        {
            return [.. Snapshot.Entities.Select(ClassMapper.ToContract)];
        }

        public CoreClass? GetClass(int classId)
        {
            return Snapshot.CoreClasses.Lookup(classId);
        }

        public EntityClass? LookupClass(int classId)
        {
            return Snapshot.Entities.Lookup(classId);
        }
    }
}
