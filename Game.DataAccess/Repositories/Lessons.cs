using Game.Abstractions.DataAccess;
using Game.DataAccess.Mapping;
using Game.DataAccess.Repositories.Caching;
using LessonEntity = Game.Infrastructure.Entities.Lesson;
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

        public bool ValidateLessonId(int lessonId)
        {
            return LookupLesson(lessonId) is not null;
        }

        public LessonEntity? LookupLesson(int lessonId)
        {
            return Snapshot.Entities.Lookup(lessonId);
        }

        public IReadOnlyList<LessonEntity> AllLessonEntities()
        {
            return Snapshot.Entities;
        }
    }
}
