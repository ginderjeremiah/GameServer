using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Game.Core.Zones;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents a player character in the game.
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The name of the player.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The level of the player.
        /// </summary>
        public required int Level { get; set; }

        /// <summary>
        /// How much EXP the player has towards their next level.
        /// </summary>
        public required int Exp { get; set; }

        /// <summary>
        /// The <see cref="Zone"/> that the player is currently in.
        /// </summary>
        public required Zone CurrentZone { get; set; }

        /// <inheritdoc cref="PlayerStatPoints"/>
        public required PlayerStatPoints StatPoints { get; set; }

        /// <summary>
        /// The player's inventory.
        /// </summary>
        public required Inventory Inventory { get; set; }

        /// <summary>
        /// The list of skills that the player has selected for battle.
        /// </summary>
        public required List<Skill> SelectedSkills { get; set; }

        /// <summary>
        /// The list of skills that the player has learned.
        /// </summary>
        public required List<Skill> Skills { get; set; }

        /// <inheritdoc cref="PlayerStatPoints.TryUpdateAttributes(IEnumerable{IAttributeUpdate})"/>
        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            return StatPoints.TryUpdateAttributes(changedAttributes);
        }

        /// <summary>
        /// Grants the player the given amount of EXP and levels up if necessary.
        /// </summary>
        /// <param name="amount"></param>
        public void GrantExp(int amount)
        {
            Exp += amount;
            if (Exp > Level * 100)
            {
                Exp -= Level * 100;
                Level++;
                StatPoints.StatPointsGained += 6;
            }
        }
    }
}
