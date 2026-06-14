using Game.Core.Attributes;
using Game.Core.Skills;

namespace Game.Core.Enemies
{
    /// <summary>
    /// A level-independent, pre-mapped enemy template. It bundles the immutable building blocks of an enemy —
    /// its attribute distributions and its available-skill loadout — so a per-encounter <see cref="Enemy"/>
    /// can be produced for a given level without re-mapping the graph on every battle setup (the gameplay-read
    /// optimization #449, here applied to enemies via #584). An <see cref="Enemy"/> cannot itself be shared
    /// like the other cached reference models because it is level-parameterized <em>and</em> carries
    /// per-encounter mutable battle-skill state; the template holds everything that is neither, and
    /// <see cref="ToEnemy"/> reuses its blocks by reference across every produced instance.
    /// </summary>
    public sealed class EnemyTemplate
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required bool IsBoss { get; init; }
        public required IReadOnlyList<AttributeDistribution> AttributeDistributions { get; init; }
        public required IReadOnlyList<Skill> AvailableSkills { get; init; }

        /// <summary>
        /// Produces a per-encounter <see cref="Enemy"/> at <paramref name="level"/>, reusing this template's
        /// shared attribute distributions and available skills. Only the level is applied here; the encounter's
        /// battle-skill selection is made on the returned instance (<see cref="Enemy.SelectBattleSkills"/> etc.).
        /// </summary>
        public Enemy ToEnemy(int level)
        {
            return new Enemy
            {
                Id = Id,
                Name = Name,
                IsBoss = IsBoss,
                Level = level,
                AttributeDistributions = AttributeDistributions,
                AvailableSkills = AvailableSkills,
            };
        }
    }
}
