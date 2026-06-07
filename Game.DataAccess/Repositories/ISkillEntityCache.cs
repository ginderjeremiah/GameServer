using SkillEntity = Game.Abstractions.Entities.Skill;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Internal access to the cached skill <em>entities</em> for data-access mappers that still need
    /// them (e.g. building domain enemies). Kept out of the public <see cref="Abstractions.DataAccess.ISkills"/>
    /// read contract, which now returns skill contracts — the entity is an implementation detail of this layer.
    /// </summary>
    internal interface ISkillEntityCache
    {
        IReadOnlyList<SkillEntity> AllSkillEntities(bool refreshCache = false);
    }
}
