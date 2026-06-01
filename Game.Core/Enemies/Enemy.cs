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

        public List<Skill> GetRandomSkills(Mulberry32 rng)
        {
            return Skills.OrderBy(s => rng.Next()).Take(4).ToList();
        }
    }
}
