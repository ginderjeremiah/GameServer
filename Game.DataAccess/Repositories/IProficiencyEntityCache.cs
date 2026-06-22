using ProficiencyEntity = Game.Infrastructure.Entities.Proficiency;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to a cached proficiency <em>entity</em> for the Content Authoring admin persistence
    /// (<see cref="LookupProficiency"/>), which needs the EF entity for existence/diff lookups. Kept out of
    /// the public <see cref="Abstractions.DataAccess.IProficiencies"/> read contract, which returns contracts.
    /// </summary>
    internal interface IProficiencyEntityCache
    {
        /// <summary>The cached proficiency entity at <paramref name="proficiencyId"/> (its zero-based index),
        /// or null if out of range.</summary>
        ProficiencyEntity? LookupProficiency(int proficiencyId);
    }
}
