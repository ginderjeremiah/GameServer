using SkillEntity = Game.Infrastructure.Entities.Skill;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached skill <em>entities</em> for data-access consumers that still need
    /// them — the domain-enemy mapper (<see cref="AllSkillEntities"/>) and the Content Authoring admin
    /// persistence (<see cref="LookupSkill"/>). Kept out of the public
    /// <see cref="Abstractions.DataAccess.ISkills"/> read contract, which returns skill contracts — the
    /// entity is an implementation detail of this layer.
    /// </summary>
    internal interface ISkillEntityCache
    {
        IReadOnlyList<SkillEntity> AllSkillEntities(bool refreshCache = false);

        /// <summary>The cached skill entity at <paramref name="skillId"/> (its zero-based index), or null if out of range.</summary>
        SkillEntity? LookupSkill(int skillId);
    }
}
