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

        public void ChangeZone(int zoneId)
        {
            CurrentZoneId = zoneId;
        }

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
            while (Exp > Level * 100)
            {
                Exp -= Level * 100;
                Level++;
                StatPoints.StatPointsGained += 6;
                RaiseEvent(new PlayerLeveledUpEvent(Id, Level, StatPoints.StatPointsGained));
            }
        }

        /// <summary>
        /// Unlocks an item for the player and raises an <see cref="ItemUnlockedEvent"/>.
        /// </summary>
        public void UnlockItem(int itemId)
        {
            Inventory.UnlockItem(itemId);
            RaiseEvent(new ItemUnlockedEvent(Id, itemId));
        }

        /// <summary>
        /// Unlocks a modifier for the player and raises a <see cref="ModUnlockedEvent"/>.
        /// </summary>
        public void UnlockMod(int itemModId)
        {
            Inventory.UnlockMod(itemModId);
            RaiseEvent(new ModUnlockedEvent(Id, itemModId));
        }

        /// <summary>
        /// Records that an enemy has been defeated and raises an <see cref="EnemyDefeatedEvent"/>.
        /// </summary>
        public void RecordEnemyDefeat(int enemyId, int expReward)
        {
            RaiseEvent(new EnemyDefeatedEvent(Id, enemyId, expReward));
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
