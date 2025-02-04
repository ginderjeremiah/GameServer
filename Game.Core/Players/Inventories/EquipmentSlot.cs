using Game.Core.Items;
using static Game.Core.EEquipmentSlot;
using static Game.Core.EItemCategory;

namespace Game.Core.Players
{
    /// <inheritdoc cref="EEquipmentSlot"/>
    public class EquipmentSlot
    {
        /// <summary>
        /// The enum value of the equipment slot.
        /// </summary>
        public EEquipmentSlot Value { get; set; }

        /// <summary>
        /// The name of the equipment slot.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The item category that is allowed to be equipped.
        /// </summary>
        public EItemCategory ItemCategory { get; set; }

        /// <summary>
        /// The item that is currently equipped in this slot.
        /// </summary>
        public Item? Item { get; set; }

        /// <summary>
        /// Creates a new equipment slot based on the given enum value.
        /// </summary>
        /// <param name="value"></param>
        public EquipmentSlot(EEquipmentSlot value)
        {
            Value = value;
            Name = value.ToString().Capitalize().SpaceWords();
            ItemCategory = GetItemCategory(value);
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
