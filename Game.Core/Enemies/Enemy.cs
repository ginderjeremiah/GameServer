using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Items;
using Game.Core.Skills;

namespace Game.Core.Enemies
{
    /// <summary>
    /// Represents an enemy that the player can encounter in the game.
    /// </summary>
    public class Enemy
    {
        /// <summary>
        /// The unique identifier of the enemy.
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// The name of the enemy.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The level of the enemy.
        /// </summary>
        public required int Level { get; set; }

        /// <summary>
        /// The distributions used to generate a set of attribute modifiers for the enemy.
        /// </summary>
        public required List<AttributeDistribution> AttributeDistributions { get; set; }

        /// <summary>
        /// The list of skills that the enemy can use.
        /// </summary>
        public required List<Skill> Skills { get; set; }

        /// <summary>
        /// The items that the enemy can drop when defeated.
        /// </summary>
        public required List<EnemyDrop> Drops { get; set; }

        /// <summary>
        /// Transforms the attribute distributions into a list of attribute modifiers.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AttributeModifier> GetAttributeModifiers()
        {
            return AttributeDistributions.Select(d => d.GetDistributionModifier(Level));
        }

        /// <summary>
        /// Gets a random selection of skills for the enemy.
        /// </summary>
        /// <param name="rng"></param>
        /// <returns></returns>
        public List<Skill> GetRandomSkills(Mulberry32 rng)
        {
            return Skills.OrderBy(s => rng.Next()).Take(4).ToList();
        }

        /// <summary>
        /// Rolls the items that the enemy will drop when defeated.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Item> RollDrops(Mulberry32 rng)
        {
            foreach (var drop in Drops.Where(d => (decimal)rng.Next() < d.DropRate))
            {
                yield return drop.Item;
            }
        }
    }
}
