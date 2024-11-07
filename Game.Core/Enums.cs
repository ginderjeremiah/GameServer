using Game.Core.CustomAttributes;
using Game.Core.Entities;
using Attribute = Game.Core.Entities.Attribute;

namespace Game.Core
{
    public enum EAttribute
    {
        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Strength = 0,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Endurance = 1,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Intellect = 2,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Agility = 3,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Dexterity = 4,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Luck = 5,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        MaxHealth = 6,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        Defense = 7,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CooldownRecovery = 8,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        DropBonus = 9,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CriticalChance = 10,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        CriticalDamage = 11,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        DodgeChance = 12,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        BlockChance = 13,

        [Metadata(nameof(Attribute.Description), "A measure of one's raw physical force.")]
        BlockReduction = 14
    }

    public enum EItemCategory
    {
        Helm = 1,
        Chest = 2,
        Leg = 3,
        Boot = 4,
        Weapon = 5,
        Accessory = 6
    }

    public enum EEquipmentSlot
    {
        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Helm)]
        HelmSlot = 0,

        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Chest)]
        ChestSlot = 1,

        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Leg)]
        LegSlot = 2,

        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Boot)]
        BootSlot = 3,

        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Weapon)]
        WeaponSlot = 4,

        [Metadata(nameof(EquipmentSlot.ItemCategoryId), EItemCategory.Accessory)]
        AccessorySlot = 5
    }

    public enum ELogSetting
    {
        [Metadata(nameof(LogSetting.DefaultValue), false)]
        Damage = 1,

        [Metadata(nameof(LogSetting.DefaultValue), false)]
        Debug = 2,

        [Metadata(nameof(LogSetting.DefaultValue), true)]
        Exp = 3,

        [Metadata(nameof(LogSetting.DefaultValue), true)]
        LevelUp = 4,

        [Metadata(nameof(LogSetting.DefaultValue), true)]
        Inventory = 5,

        [Metadata(nameof(LogSetting.DefaultValue), true)]
        EnemyDefeated = 6
    }

    public enum EItemModSlotType
    {
        Component = 1,
        Prefix = 2,
        Suffix = 3
    }

    public enum ETagCategory
    {
        Acessory = 1,
        Armor = 2,
        Magical = 3,
        Material = 4,
        Modification = 5,
        Usage = 6,
        Weapon = 7,
    }
}
