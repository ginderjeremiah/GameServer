using LessonEntity = Game.Infrastructure.Entities.Lesson;

namespace Game.DataAccess.Repositories
{
    /// <summary>Internal access to cached lesson <em>entities</em> for the Content Authoring admin
    /// persistence, which needs the EF entities for existence/diff lookups. Kept out of the public
    /// <see cref="Abstractions.DataAccess.ILessons"/> read contract, which returns contracts.</summary>
    internal interface ILessonEntityCache
    {
        /// <summary>The cached lesson entity at <paramref name="lessonId"/> (its zero-based index), or null if
        /// out of range.</summary>
        LessonEntity? LookupLesson(int lessonId);

        /// <summary>Every cached lesson entity (each with its steps loaded), for graph-wide authoring checks
        /// such as key-uniqueness validation.</summary>
        IReadOnlyList<LessonEntity> AllLessonEntities();
    }
}
