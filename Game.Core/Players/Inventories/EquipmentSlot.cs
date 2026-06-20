using Game.Core.Items;
using static Game.Core.EEquipmentSlot;
using static Game.Core.EItemCategory;

namespace Game.Core.Players.Inventories
{
    /// <inheritdoc cref="EEquipmentSlot"/>
    public class EquipmentSlot
    {
        /// <summary>
        /// The enum value of the equipment slot.
        /// </summary>
        public EEquipmentSlot Value { get; set; }

        /// <summary>
        /// The name of the equipment slot, derived from <see cref="Value"/>.
        /// </summary>
        public string Name => Value.ToString().Capitalize().SpaceWords();

        /// <summary>
        /// The item category that is allowed to be equipped, derived from <see cref="Value"/>.
        /// </summary>
        public EItemCategory ItemCategory => GetItemCategory(Value);

        /// <summary>
        /// The item that is currently equipped in this slot, or null when the slot is empty. The single
        /// source of truth for what the slot holds; <see cref="ItemId"/> is derived from it so the two can
        /// never desync.
        /// </summary>
        public Item? Item { get; set; }

        /// <summary>
        /// The item ID of the equipped item, or null when the slot is empty. Derived from
        /// <see cref="Item"/> rather than stored independently.
        /// </summary>
        public int? ItemId => Item?.Id;

        /// <summary>
        /// Creates a new equipment slot based on the given enum value.
        /// </summary>
        /// <param name="value"></param>
        public EquipmentSlot(EEquipmentSlot value)
        {
            Value = value;
        }

        /// <summary>
        /// Equips the given <paramref name="item"/> into this slot.
        /// </summary>
        public void Set(Item item)
        {
            Item = item;
        }

        /// <summary>
        /// Empties this slot.
        /// </summary>
        public void Clear()
        {
            Item = null;
        }

        /// <summary>
        /// Gets the <see cref="EItemCategory"/> associated with the given <paramref name="slot"/>.
        /// </summary>
        /// <param name="slot"></param>
        /// <exception cref="ArgumentException"></exception>
        private static EItemCategory GetItemCategory(EEquipmentSlot slot)
        {
            return slot switch
            {
                HelmSlot => Helm,
                ChestSlot => Chest,
                LegSlot => Leg,
                BootSlot => Boot,
                WeaponSlot => Weapon,
                AccessorySlot => Accessory,
                _ => throw new ArgumentException($"The given equipment slot has no associated category: {slot}", nameof(slot))
            };
        }
    }
}
