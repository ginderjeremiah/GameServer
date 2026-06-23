using PathEntity = Game.Infrastructure.Entities.Path;
using ProficiencyEntity = Game.Infrastructure.Entities.Proficiency;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to cached proficiency/path <em>entities</em> for the Content Authoring admin
    /// persistence, which needs the EF entities for existence/diff lookups. Kept out of the public
    /// <see cref="Abstractions.DataAccess.IProficiencies"/> read contract, which returns contracts.
    /// </summary>
    internal interface IProficiencyEntityCache
    {
        /// <summary>The cached proficiency entity at <paramref name="proficiencyId"/> (its zero-based index),
        /// or null if out of range.</summary>
        ProficiencyEntity? LookupProficiency(int proficiencyId);

        /// <summary>The cached path entity at <paramref name="pathId"/> (its zero-based index), or null if out
        /// of range.</summary>
        PathEntity? LookupPath(int pathId);

        /// <summary>The cached proficiency that is the tier at <paramref name="ordinal"/> of path
        /// <paramref name="pathId"/>, or null if no such tier exists.</summary>
        ProficiencyEntity? LookupProficiencyByTier(int pathId, int ordinal);

        /// <summary>Every cached proficiency entity (each with its prerequisites loaded), for graph-wide
        /// authoring checks such as prerequisite cycle detection.</summary>
        IReadOnlyList<ProficiencyEntity> AllProficiencyEntities();
    }
}
