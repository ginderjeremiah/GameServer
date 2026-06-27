using Game.Core.Items;

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
        /// A derived game attribute. A decimal probability (0.05 = 5%) that an attack lands a critical hit,
        /// compared directly against the battle RNG draw. Sourced from Dexterity/Luck (player-only).
        /// </summary>
        CriticalChance = 10,

        /// <summary>
        /// A derived game attribute. A base-≥1 multiplier (base 1.5) read directly: on a critical hit the raw
        /// damage is multiplied by it before Defense is subtracted, so it can punch through Defense.
        /// </summary>
        CriticalDamage = 11,

        /// <summary>
        /// A derived game attribute. A decimal probability (0.05 = 5%) to fully ignore an incoming attack,
        /// compared directly against the battle RNG draw. Sourced from Agility (player-only).
        /// </summary>
        DodgeChance = 12,

        /// <summary>
        /// A derived game attribute. A decimal probability (0.05 = 5%) to block part of an incoming attack,
        /// compared directly against the battle RNG draw. Sourced from Endurance (player-only).
        /// </summary>
        BlockChance = 13,

        /// <summary>
        /// A derived game attribute. A flat reduction (base 2) applied alongside Defense when an incoming
        /// attack is blocked.
        /// </summary>
        BlockReduction = 14,

        /// <summary>
        /// A per-second attribute representing the amount of damage taken each second from damage-over-time
        /// effects. Consumed by the end-of-tick DoT/HoT simulator phase (bypasses Defense).
        /// </summary>
        DamageTakenPerSecond = 15,

        /// <summary>
        /// A per-second attribute representing the amount of health restored each second from heal-over-time
        /// effects. Consumed by the end-of-tick DoT/HoT simulator phase (capped at MaxHealth).
        /// </summary>
        HealthRegenPerSecond = 16
    }

    /// <summary>
    /// The display/classification taxonomy for an <see cref="EAttribute"/>, surfaced on the attribute
    /// reference data so the client groups and renders attributes from a single backend source of truth.
    /// This is a display-only taxonomy and is intentionally kept distinct from the core/derived power-calc
    /// invariant (the <see cref="Primary"/> set is expected to equal the core attribute set, but the two
    /// are not collapsed).
    /// </summary>
    [ClientMirrored]
    public enum EAttributeType
    {
        /// <summary>A core, directly-allocatable attribute (STR/END/INT/AGI/DEX/LUK).</summary>
        Primary = 1,

        /// <summary>
        /// An aggregate stat computed from a base/derived formula (MaxHealth, Defense, CooldownRecovery,
        /// and the crit/dodge/block set).
        /// </summary>
        Secondary = 2,

        /// <summary>
        /// A transient per-second combat channel fed only by effects/gear (DamageTakenPerSecond,
        /// HealthRegenPerSecond).
        /// </summary>
        Status = 3,
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
        EnemyDefeated = 6,

        /// <summary>
        /// Logs for skill-effect activity in battle: timed buff/debuff application and
        /// per-second damage-over-time / heal-over-time summaries.
        /// </summary>
        SkillEffect = 7,

        /// <summary>
        /// Logs for proficiency progression earned from won battles: XP gained, level-ups,
        /// milestones reached, and newly-opened proficiencies.
        /// </summary>
        Proficiency = 8
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
    /// The possible categories for a tag.
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
    /// How a statistic's recorded value is aggregated across the battles that report it. A derived domain
    /// fact of the statistic type (see <see cref="Progress.StatisticType"/>), so it is the single source of
    /// truth for both the recording mutator and a challenge's <see cref="EChallengeGoalComparison"/>.
    /// </summary>
    public enum EAggregationKind
    {
        /// <summary>
        /// Each report is added to a running total (e.g. kills, damage dealt). Higher is better, so a
        /// backing challenge completes once the value is at least the goal.
        /// </summary>
        Sum = 1,

        /// <summary>
        /// Only the largest reported value is kept (e.g. highest single attack). Higher is better, so a
        /// backing challenge completes once the value is at least the goal.
        /// </summary>
        Max = 2,

        /// <summary>
        /// Only the smallest reported value is kept (e.g. fastest victory). Lower is better, so a backing
        /// challenge completes once the value is at or below the goal.
        /// </summary>
        Min = 3,
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
        CriticalHits = 16,
        CriticalDamageDealt = 17,
        AttacksDodged = 18,
        DamageDodged = 19,
        AttacksBlocked = 20,
        DamageBlocked = 21,
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

        /// <summary>
        /// The modifier came from a player's proficiency level (a per-level/milestone bonus baked into the
        /// battle snapshot at battle start).
        /// </summary>
        Proficiency = 8,

        /// <summary>
        /// The modifier came from the player's class signature passive — the durable combat-identity bonus
        /// (flat or attribute-scaled) composed into the battler at assembly (spike #1126 area E).
        /// </summary>
        Class = 9,
    }

    /// <summary>
    /// Determines which battler a <see cref="Skills.SkillEffect"/> is applied to when its skill fires.
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
    /// The channels a skill is <em>allowed</em> to be acquired through. A declared authoring intent, not a
    /// record of what exists: a skill may be <see cref="Item"/>-flagged with no item granting it yet. The
    /// references (a proficiency milestone reward or tree-seed skill, an enemy's skill pool, an item's grant)
    /// are the reality; backend authoring validation bridges the two. "Enemy-Only" is <see cref="Enemy"/> set
    /// with the others clear — an <see cref="Item"/>-only skill can never be a proficiency grant, which is what
    /// guarantees it can be obtained solely by equipping the granting item.
    /// </summary>
    [Flags]
    [ClientMirrored]
    public enum ESkillAcquisition
    {
        /// <summary>Not acquirable through any channel.</summary>
        None = 0,

        /// <summary>Permanently granted to the player and added to the unlocked loadout pool — by a proficiency
        /// milestone reward or a tree-seed skill (challenges no longer grant skills; spike #982 area D/I).</summary>
        Player = 1,

        /// <summary>Grantable by an equipped item.</summary>
        Item = 2,

        /// <summary>Assignable to an enemy's skill pool.</summary>
        Enemy = 4,
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
