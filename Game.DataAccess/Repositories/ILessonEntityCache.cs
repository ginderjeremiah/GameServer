using Lesson = Game.Infrastructure.Entities.Lesson;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached lesson <em>entities</em> for the Content Authoring admin persistence, which
    /// needs the EF entities for existence/diff lookups. Kept out of the public
    /// <see cref="Abstractions.DataAccess.ILessons"/> read contract, which returns lesson contracts — the entity
    /// is an implementation detail of this layer.
    /// </summary>
    internal interface ILessonEntityCache
    {
        /// <summary>The cached lesson entity at <paramref name="id"/> (its zero-based index), or null if out of range.</summary>
        Lesson? LookupLesson(int id);

        /// <summary>Every cached lesson entity (with its steps loaded).</summary>
        IReadOnlyList<Lesson> AllLessonEntities();
    }
}
