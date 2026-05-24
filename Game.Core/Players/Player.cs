using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Events;
using Game.Core.Items;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.Players
{
    /// <summary>
    /// Represents a player character in the game.
    /// </summary>
    public class Player : AggregateRoot
    {
        public required int Id { get; set; }

        public required string Name { get; set; }

        public required int Level { get; set; }

        public required int Exp { get; set; }

        public required int CurrentZoneId { get; set; }

        public required PlayerStatPoints StatPoints { get; set; }

        public required Inventory Inventory { get; set; }

        public required List<Skill> SelectedSkills { get; set; }

        public required List<Skill> Skills { get; set; }

        public bool TryUpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            return StatPoints.TryUpdateAttributes(changedAttributes);
        }

        /// <summary>
        /// Awards experience to the player. Raises <see cref="PlayerLeveledUpEvent"/> if a
        /// level-up occurs.
        /// </summary>
        public void GrantExp(int amount)
        {
            Exp += amount;
            if (Exp > Level * 100)
            {
                Exp -= Level * 100;
                Level++;
                StatPoints.StatPointsGained += 6;
                RaiseEvent(new PlayerLeveledUpEvent(Id, Level, StatPoints.StatPointsGained));
            }
        }

        /// <summary>
        /// Adds <paramref name="item"/> to the inventory and raises an
        /// <see cref="ItemAcquiredEvent"/>.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <param name="inventoryItemId">The database record ID of the persisted inventory item.</param>
        public void AddInventoryItem(Item item, int inventoryItemId)
        {
            var slotNumber = Inventory.TryAddItem(item, inventoryItemId);
            RaiseEvent(new ItemAcquiredEvent(Id, item, inventoryItemId, slotNumber));
        }

        /// <summary>
        /// Records that an enemy has been defeated and raises an <see cref="EnemyDefeatedEvent"/>.
        /// </summary>
        /// <param name="enemyId">The defeated enemy's identifier.</param>
        /// <param name="expReward">Experience awarded.</param>
        /// <param name="drops">Items that dropped from the enemy.</param>
        public void RecordEnemyDefeat(int enemyId, int expReward, IReadOnlyList<Item> drops)
        {
            RaiseEvent(new EnemyDefeatedEvent(Id, enemyId, expReward, drops));
        }

        public IEnumerable<AttributeModifier> GetAllModifiers()
        {
            return StatPoints.ToAttributeModifiers()
                .Concat(Inventory.GetEquippedAttributeModifiers());
        }

        public AttributeCollection GetAttributes()
        {
            return new AttributeCollection(GetAllModifiers());
        }
    }
}
