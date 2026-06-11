using Game.Core.Items;
using Game.Core.Tags;

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
        Strength = 0,

        /// <summary>
        /// One of the core game attributes. Primarily determines a characters <see cref="MaxHealth"/> and <see cref="Defense"/> attributes.
        /// </summary>
        Endurance = 1,

        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of magical skills.
        /// </summary>
        Intellect = 2,

        /// <summary>
        /// One of the core game attributes. Primarily determines a character;s <see cref="CooldownRecovery"/> and also contributes to <see cref="Defense"/>.
        /// </summary>
        Agility = 3,

        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of some physical skills and contributes to <see cref="CooldownRecovery"/>.
        /// </summary>
        Dexterity = 4,

        /// <summary>
        /// One of the core game attributes. Will eventually impact various RNG-based calculations.
        /// </summary>
        Luck = 5,

        /// <summary>
        /// A derived game attribute. Determines a character's health at the start of a battle.
        /// </summary>
        MaxHealth = 6,

        /// <summary>
        /// A derived game attribute. Represents a flat reduction for all incoming damage.
        /// </summary>
        Defense = 7,

        /// <summary>
        /// A derived game attribute. Represents a % multiplier to the rate that skills become available again after being used.
        /// </summary>
        CooldownRecovery = 8,

        /// <summary>
        /// OBSOLETE. Previously represented a % multiplier to the rate at which items are dropped from enemies.
        /// </summary>
        [Obsolete("Drop system has been replaced by challenge-based unlocks.")]
        DropBonus = 9,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to deal increased damage with an attack.
        /// </summary>
        CriticalChance = 10,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents an additive % increase in the amount of damage dealt when a critical hit occurs.
        /// </summary>
        CriticalDamage = 11,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to ignore all damage from an incoming attack.
        /// </summary>
        DodgeChance = 12,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a % change to ignore part of the damage from an incoming attack.
        /// </summary>
        BlockChance = 13,

        /// <summary>
        /// CURRENTLY UNUSED. A derived game attribute. Represents a flat reduction for damage received when a block occurs.
        /// </summary>
        BlockReduction = 14
    }

    /// <summary>
    /// Represents the category for an item. Some items can only be equipped based on their category.
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
    [ClientMirrored]
    public enum EEquipmentSlot
    {
        /// <summary>
        /// A slot for a helm. Requires that the item have the category <see cref="EItemCategory.Helm"/>.
        /// </summary>
        HelmSlot = 0,

        /// <summary>
        /// A slot for chestwear. Requires that the item have the category <see cref="EItemCategory.Chest"/>.
        /// </summary>
        ChestSlot = 1,

        /// <summary>
        /// A slot for legwear. Requires that the item have the category <see cref="EItemCategory.Leg"/>.
        /// </summary>
        LegSlot = 2,

        /// <summary>
        /// A slot for boots. Requires that the item have the category <see cref="EItemCategory.Boot"/>.
        /// </summary>
        BootSlot = 3,

        /// <summary>
        /// A slot for a weapon. Requires that the item have the category <see cref="EItemCategory.Weapon"/>.
        /// </summary>
        WeaponSlot = 4,

        /// <summary>
        /// A slot for an accessory. Requires that the item have the category <see cref="EItemCategory.Accessory"/>.
        /// </summary>
        AccessorySlot = 5
    }

    /// <summary>
    /// Represents a kind of log which can be filtered out within the UI of the game.
    /// </summary>
    public enum ELogType
    {
        /// <summary>
        /// Logs when damage is dealt by any character in battle.
        /// </summary>
        Damage = 1,

        /// <summary>
        /// Logs for any info that is not pertinent to a player.
        /// </summary>
        Debug = 2,

        /// <summary>
        /// Logs for when the player gains exp.
        /// </summary>
        Exp = 3,

        /// <summary>
        /// Logs for when the player levels up.
        /// </summary>
        LevelUp = 4,

        /// <summary>
        /// Logs for when an item is added to the inventory or cannot be added because the inventory is full.
        /// </summary>
        ItemFound = 5,

        /// <summary>
        /// Logs for when an enemy has been defeated.
        /// </summary>
        EnemyDefeated = 6
    }

    /// <summary>
    /// Represents the type of an <see cref="ItemMod"/>
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

    /// <summary>
    /// Represents the type of a modifier (the way it is applied).
    /// </summary>
    [ClientMirrored]
    public enum EModifierType
    {
        /// <summary>
        /// The modifier is added to the value before applying <see cref="Multiplicative"/> modifiers.
        /// </summary>
        Additive = 1,

        /// <summary>
        /// The modifier amplifies the final value.
        /// </summary>
        Multiplicative = 2,
    }

    /// <summary>
    /// Represents the type of challenge condition that must be met to unlock a reward.
    /// </summary>
    public enum EChallengeType
    {
        EnemiesKilled = 1,
        BossesDefeated = 2,
        ZonesCleared = 3,
        TimeTrial = 4,
        LevelReached = 5,
        DamageDealt = 6,
        BattlesWon = 7,
        SkillsUsed = 8,
    }

    /// <summary>
    /// Represents how a challenge's tracked value is compared against its goal to determine completion.
    /// This is intrinsic to the challenge type and is not persisted.
    /// </summary>
    public enum EChallengeGoalComparison
    {
        /// <summary>
        /// The challenge completes once the tracked value reaches at least the goal. Used for
        /// accumulating statistics where higher is better (e.g. kills, damage dealt).
        /// </summary>
        AtLeast = 1,

        /// <summary>
        /// The challenge completes once the tracked value is at or below the goal. Used for
        /// statistics where lower is better (e.g. a time trial against the fastest victory time).
        /// </summary>
        AtMost = 2,
    }

    /// <summary>
    /// Represents a type of player statistic that is tracked over time.
    /// </summary>
    public enum EStatisticType
    {
        EnemiesKilled = 1,
        BossesDefeated = 2,
        ZonesCleared = 3,
        DamageDealt = 4,
        HighestSingleAttackDamage = 5,
        DamageTaken = 6,
        DamageHealed = 7,
        EnemiesEncountered = 8,
        BattlesWon = 9,
        BattlesLost = 10,
        PlayerDeaths = 11,
        TotalBattleTime = 12,
        FastestVictory = 13,
        SkillsUsed = 14,
        BattlesAbandoned = 15,
    }

    /// <summary>
    /// Represents the type of entity that a statistic or challenge may reference.
    /// </summary>
    public enum EEntityType
    {
        None = 0,
        Enemy = 1,
        Zone = 2,
        Skill = 3,
    }

    /// <summary>
    /// Represents the source that contributed the attribute modifier.
    /// </summary>
    [ClientMirrored]
    public enum EAttributeModifierSource
    {
        /// <summary>
        /// The modifier represents the base value of the attribute.
        /// </summary>
        BaseValue = 1,

        /// <summary>
        /// The modifier came from the player's base stat allocations.
        /// </summary>
        PlayerStatPoints = 2,

        /// <summary>
        /// The modifier came from the distribution of attribute points.
        /// </summary>
        AttributeDistribution = 3,

        /// <summary>
        /// The modifier is derived from the value of another attribute.
        /// </summary>
        Derived = 4,

        /// <summary>
        /// The modifier came from an item.
        /// </summary>
        Item = 5,

        /// <summary>
        /// The modifier came from an item mod.
        /// </summary>
        ItemMod = 6,

        /// <summary>
        /// The modifier came from an active skill effect (a timed buff/debuff).
        /// </summary>
        SkillEffect = 7,
    }

    /// <summary>
    /// Determines which battler a <see cref="SkillEffect"/> is applied to when its skill fires.
    /// </summary>
    [ClientMirrored]
    public enum ESkillEffectTarget
    {
        /// <summary>
        /// The effect is applied to the battler that used the skill.
        /// </summary>
        Self = 1,

        /// <summary>
        /// The effect is applied to the opposing battler.
        /// </summary>
        Opponent = 2,
    }

    /// <summary>
    /// Represents the rarity of something (which loosely corresponds to its strength and the requirements to obtain it).
    /// </summary>
    public enum ERarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6,
    }

    /// <summary>
    /// Represents an access role that can be granted to a user. Roles are intrinsic reference data
    /// (encoded in the application and used to gate functionality) rather than free-form data.
    /// </summary>
    public enum ERole
    {
        /// <summary>
        /// Grants access to the administrative tooling endpoints.
        /// </summary>
        Admin = 1,
    }
}
