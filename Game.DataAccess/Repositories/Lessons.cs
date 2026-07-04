using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using Game.Infrastructure.Entities;
using Contracts = Game.Abstractions.Contracts;

namespace Game.DataAccess.Repositories
{
    internal class Lessons(LessonsCacheHolder holder) : ILessons, ILessonEntityCache
    {
        // Read the immutable snapshot once per logical operation (docs/backend.md → Reference-data snapshot
        // read-once idiom) so a build-then-swap between reads can't mix an old and a new snapshot in one call.
        private LessonSnapshot Snapshot => holder.Current;

        // The snapshot instance changes on every build-then-swap, so it doubles as the content-version key.
        public object VersionKey => Snapshot;

        public List<Contracts.Lesson> AllLessons()
        {
            return [.. Snapshot.Entities.Select(LessonMapper.ToContract)];
        }

        public Lesson? LookupLesson(int id)
        {
            return Snapshot.Entities.Lookup(id);
        }

        public IReadOnlyList<Lesson> AllLessonEntities()
        {
            return Snapshot.Entities;
        }
    }
}
