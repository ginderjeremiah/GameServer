using ClassEntity = Game.Infrastructure.Entities.Class;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached class <em>entities</em> for the Content Authoring admin persistence
    /// (<see cref="Repositories.Admin.AdminClasses"/>), which needs the EF entity for existence/diff lookups.
    /// Kept out of the public <see cref="Abstractions.DataAccess.IClasses"/> read contract — the entity is an
    /// implementation detail of this layer.
    /// </summary>
    internal interface IClassEntityCache
    {
        /// <summary>The cached class entity at <paramref name="classId"/> (its zero-based index), or null if out of range.</summary>
        ClassEntity? LookupClass(int classId);
    }
}
