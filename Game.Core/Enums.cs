using Game.Core.BattleSimulation;
using Game.Core.CustomAttributes;
using Game.Core.Entities;
using Attribute = Game.Core.Entities.Attribute;

namespace Game.Core
{
    /// <summary>
    /// Represents a stat or modifier used in the game.
    /// </summary>
    public enum EAttribute
    {
        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of some physical skills and contributes to <see cref="MaxHealth"/>.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Strength = 0,

        /// <summary>
        /// One of the core game attributes. Primarily determines a characters <see cref="MaxHealth"/> and <see cref="Defense"/> attributes.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Endurance = 1,

        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of magical skills.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Intellect = 2,

        /// <summary>
        /// One of the core game attributes. Primarily determines a character;s <see cref="CooldownRecovery"/> and also contributes to <see cref="Defense"/>.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Agility = 3,

        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of some physical skills and contributes to <see cref="CooldownRecovery"/>.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Dexterity = 4,

        /// <summary>
        /// One of the core game attributes. Primarily determines a character's <see cref="DropBonus"/>.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Luck = 5,

        /// <summary>
        /// A derived game attribute. Determines the <see cref="Battler.CurrentHealth"/> at the start of a battle.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        MaxHealth = 6,

        /// <summary>
        /// A derived game attribute. Represents a flat reduction for all incoming damage.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Defense = 7,

        /// <summary>
        /// A derived game attribute. Represents a % multiplier to the rate that skills become available again after being used.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CooldownRecovery = 8,

        /// <summary>
        /// A derived game attribute. Represents a % multiplier to the rate at which items are dropped from enemies.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        DropBonus = 9,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to deal increased damage with an attack.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CriticalChance = 10,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents an additive % increase in the amount of damage dealt when a critical hit occurs.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CriticalDamage = 11,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to ignore all damage from an incoming attack.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        DodgeChance = 12,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to ignore part of the damage from an incoming attack.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        BlockChance = 13,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a flat reduction for damage received when a block occurs.
        /// </summary>
        [EntityProperty(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        BlockReduction = 14
    }

    /// <summary>
    /// Represents the categories that items. Some items can only be equipped based on their category.
    /// </summary>
    public enum EItemCategory
    {
        /// <summary>
        /// Any sort of gear that provide protection for one's head.
        /// </summary>
        Helm = 1,

        /// <summary>
        /// Any sort of gear that provide protection for one's chest.
        /// </summary>
        Chest = 2,

        /// <summary>
        /// Any sort of gear that provide protection for one's legs.
        /// </summary>
        Leg = 3,

        /// <summary>
        /// Any sort of gear that provide protection for one's feet.
        /// </summary>
        Boot = 4,

        /// <summary>
        /// Any sort of gear that can directly be used to deal damage.
        /// </summary>
        Weapon = 5,

        /// <summary>
        /// Any sort of gear that does not directly deal damage or provide protection to the body.
        /// </summary>
        Accessory = 6
    }

    /// <summary>
    /// Represents a slot for items to be equipped to.
    /// </summary>
    public enum EEquipmentSlot
    {
        /// <summary>
        /// A slot for a helm. Requires that the item have the category <see cref="EItemCategory.Helm"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Helm)]
        HelmSlot = 0,

        /// <summary>
        /// A slot for chestwear. Requires that the item have the category <see cref="EItemCategory.Chest"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Chest)]
        ChestSlot = 1,

        /// <summary>
        /// A slot for legwear. Requires that the item have the category <see cref="EItemCategory.Leg"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Leg)]
        LegSlot = 2,

        /// <summary>
        /// A slot for boots. Requires that the item have the category <see cref="EItemCategory.Boot"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Boot)]
        BootSlot = 3,

        /// <summary>
        /// A slot for a weapon. Requires that the item have the category <see cref="EItemCategory.Weapon"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Weapon)]
        WeaponSlot = 4,

        /// <summary>
        /// A slot for an accessory. Requires that the item have the category <see cref="EItemCategory.Accessory"/>.
        /// </summary>
        [EntityProperty(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Accessory)]
        AccessorySlot = 5
    }

    /// <summary>
    /// Represents a kind of log which can be filtered out within the game.
    /// </summary>
    public enum ELogSetting
    {
        /// <summary>
        /// Logs when damage is dealt by any character in battle.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), false)]
        Damage = 1,

        /// <summary>
        /// Logs for any info that is not pertinent to a player.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), false)]
        Debug = 2,

        /// <summary>
        /// Logs for when the player gains exp.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), true)]
        Exp = 3,

        /// <summary>
        /// Logs for when the player levels up.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), true)]
        LevelUp = 4,

        /// <summary>
        /// Logs for when an item is added to the inventory or cannot be added because the inventory is full.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), true)]
        Inventory = 5,

        /// <summary>
        /// Logs for when an enemy has been defeated.
        /// </summary>
        [EntityProperty(nameof(LogSetting.DefaultValue), true)]
        EnemyDefeated = 6
    }

    /// <summary>
    /// Represents the type of an <see cref="ItemMod"/>,
    /// </summary>
    public enum EItemModType
    {
        /// <summary>
        /// An item mod that acts as an additional component to the item.
        /// </summary>
        Component = 1,

        /// <summary>
        /// An item mod that describes the item as a word or adjective before the item name.
        /// </summary>
        Prefix = 2,

        /// <summary>
        /// An item mod that describes the item as a word or adjective after the item name.
        /// </summary>
        Suffix = 3
    }

    /// <summary>
    /// The possible categories for a <see cref="Tag"/>.
    /// </summary>
    public enum ETagCategory
    {
        /// <summary>
        /// The tag is for an item with category <see cref="EItemCategory.Accessory"/>.
        /// </summary>
        Accessory = 1,

        /// <summary>
        /// The tag is for an item with categories <see cref="EItemCategory.Helm"/>, <see cref="EItemCategory.Chest"/>,
        /// <see cref="EItemCategory.Leg"/>, <see cref="EItemCategory.Boot"/>.
        /// </summary>
        Armor = 2,

        /// <summary>
        /// The tag is for anything that can be considered magical in nature.
        /// </summary>
        Magical = 3,

        /// <summary>
        /// The tag is for something that can be considered a type of material.
        /// </summary>
        Material = 4,

        /// <summary>
        /// The tag is for something that can be considered a modification to something else.
        /// </summary>
        Modification = 5,

        /// <summary>
        /// The tag is for something that relates to the usage of an item.
        /// </summary>
        Usage = 6,

        /// <summary>
        /// The tag is for an item with category <see cref="EItemCategory.Weapon"/>.
        /// </summary>
        Weapon = 7,
    }
}
