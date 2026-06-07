using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;

namespace Game.Core.Enemies
{
    /// <summary>
    /// Represents an enemy that the player can encounter in the game.
    /// </summary>
    public class Enemy
    {
        /// <summary>The maximum number of skills an enemy brings into a battle.</summary>
        private const int MaxBattleSkills = 4;

        public required int Id { get; init; }
        public required string Name { get; init; }
        public required int Level { get; init; }
        public required bool IsBoss { get; init; }
        public required List<AttributeDistribution> AttributeDistributions { get; init; }
        public required List<Skill> Skills { get; set; }

        public IEnumerable<AttributeModifier> GetAttributeModifiers()
        {
            return AttributeDistributions.Select(d => d.GetDistributionModifier(Level));
        }

        /// <summary>
        /// Selects this enemy's loadout for a battle: up to <see cref="MaxBattleSkills"/> skills drawn
        /// from its available skills. The selection is derived deterministically from <paramref name="seed"/>,
        /// so the same seed always produces the same loadout. This lets the battle be reconstructed for
        /// validation and keeps the skills sent to the client in step with the server's re-simulation.
        /// </summary>
        public void SelectBattleSkills(uint seed)
        {
            var rng = new Mulberry32(seed);
            Skills = Skills.OrderBy(_ => rng.Next()).Take(MaxBattleSkills).ToList();
        }
    }
}
