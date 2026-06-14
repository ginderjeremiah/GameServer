using SkillEntity = Game.Infrastructure.Entities.Skill;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to a cached skill <em>entity</em> for the Content Authoring admin persistence
    /// (<see cref="LookupSkill"/>), which needs the EF entity for existence/diff lookups. Kept out of the
    /// public <see cref="Abstractions.DataAccess.ISkills"/> read contract, which returns skill contracts —
    /// the entity is an implementation detail of this layer.
    /// </summary>
    internal interface ISkillEntityCache
    {
        /// <summary>The cached skill entity at <paramref name="skillId"/> (its zero-based index), or null if out of range.</summary>
        SkillEntity? LookupSkill(int skillId);
    }
}
