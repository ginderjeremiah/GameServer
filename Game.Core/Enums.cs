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
        /// One of the core game attributes. Primarily determines a characters <see cref="MaxHealth"/> and <see cref="Toughness"/> attributes.
        /// </summary>
        Endurance = 1,

        /// <summary>
        /// One of the core game attributes. Primarily determines the damage of magical skills.
        /// </summary>
        Intellect = 2,

        /// <summary>
        /// One of the core game attributes. Primarily determines a character's <see cref="CooldownRecovery"/> and <see cref="DodgeChance"/>.
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
        /// A derived game attribute. Bulk mitigation sourced from Endurance, applied as a diminishing-returns
        /// percentage — <c>Toughness / (Toughness + K·attackerLevel)</c> — that multiplies incoming direct hits.
        /// Effective HP is linear in this stat (each point is worth a constant % of EHP) while the reduction
        /// itself asymptotes below 100% (no immunity, no breakpoint). The per-level scale K is
        /// <see cref="GameConstants.ToughnessMitigationConstant"/>; see <see cref="Battle.Battler.ComputeNetDamage"/>.
        /// </summary>
        Toughness = 7,

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
        /// damage is multiplied by it before mitigation, so it can punch through <see cref="Toughness"/>.
        /// </summary>
        CriticalDamage = 11,

        /// <summary>
        /// A derived game attribute. A decimal probability (0.05 = 5%) to fully ignore an incoming attack,
        /// compared directly against the battle RNG draw. Sourced from Agility (player-only).
        /// </summary>
        DodgeChance = 12,

        // 13 and 14 are retired (the former BlockChance / BlockReduction, removed with the Block mechanic in
        // spike #1330). The enum is intrinsic, DB-backed reference data, so the ordinals are left as a gap
        // rather than reused; the deterministic damage-reflection that replaced Block is DamageReflection below.

        /// <summary>
        /// A per-second accumulator for bleed damage-over-time (spike #1320). Consumed by the end-of-tick
        /// DoT phase, which applies the bearer's live bleed resistance and bypasses mitigation. The DoT type is
        /// encoded by which accumulator an effect targets — there is no separate type field on a skill effect.
        /// Reuses the slot of the former single <c>DamageTakenPerSecond</c> channel; <see cref="PoisonDamagePerSecond"/>
        /// and <see cref="BurnDamagePerSecond"/> append after the amp/resist block (the enum grows append-only).
        /// </summary>
        BleedDamagePerSecond = 15,

        /// <summary>
        /// A per-second attribute representing the amount of health restored each second from heal-over-time
        /// effects. Consumed by the end-of-tick DoT/HoT simulator phase (capped at MaxHealth). HoT stays
        /// typeless (spike #1320).
        /// </summary>
        HealthRegenPerSecond = 16,

        // Damage-type amplification / resistance attributes (spike #1320). One amplification + one resistance
        // per damage-type key (the eight leaf types plus the Elemental / DoT cross-cutting categories), in the
        // canonical key order used by the applies() map. Decimal-percentage convention (0.30 = 30%), base 0, so
        // an untyped or unmodified battler reads 0 everywhere. Inert in V1: nothing reads them until the damage
        // pipeline (Area B/C) does — the #178 foundation pattern. See <see cref="Attributes.DamageTypes"/>.

        /// <summary>Amplifies physical damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        PhysicalAmplification = 17,

        /// <summary>Resists physical damage taken by the defender. Decimal-percentage, base 0.</summary>
        PhysicalResistance = 18,

        /// <summary>Amplifies fire damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        FireAmplification = 19,

        /// <summary>Resists fire damage taken by the defender. Decimal-percentage, base 0.</summary>
        FireResistance = 20,

        /// <summary>Amplifies water damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        WaterAmplification = 21,

        /// <summary>Resists water damage taken by the defender. Decimal-percentage, base 0.</summary>
        WaterResistance = 22,

        /// <summary>Amplifies earth damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        EarthAmplification = 23,

        /// <summary>Resists earth damage taken by the defender. Decimal-percentage, base 0.</summary>
        EarthResistance = 24,

        /// <summary>Amplifies wind damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        WindAmplification = 25,

        /// <summary>Resists wind damage taken by the defender. Decimal-percentage, base 0.</summary>
        WindResistance = 26,

        /// <summary>Amplifies bleed damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        BleedAmplification = 27,

        /// <summary>Resists bleed damage taken by the defender. Decimal-percentage, base 0.</summary>
        BleedResistance = 28,

        /// <summary>Amplifies poison damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        PoisonAmplification = 29,

        /// <summary>Resists poison damage taken by the defender. Decimal-percentage, base 0.</summary>
        PoisonResistance = 30,

        /// <summary>Amplifies burn damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        BurnAmplification = 31,

        /// <summary>Resists burn damage taken by the defender. Decimal-percentage, base 0.</summary>
        BurnResistance = 32,

        /// <summary>Amplifies all elemental (fire/water/earth/wind) damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        ElementalAmplification = 33,

        /// <summary>Resists all elemental (fire/water/earth/wind) damage taken by the defender. Decimal-percentage, base 0.</summary>
        ElementalResistance = 34,

        /// <summary>Amplifies all damage-over-time (bleed/poison/burn) damage dealt by the attacker. Decimal-percentage, base 0.</summary>
        DotAmplification = 35,

        /// <summary>Resists all damage-over-time (bleed/poison/burn) damage taken by the defender. Decimal-percentage, base 0.</summary>
        DotResistance = 36,

        /// <summary>
        /// A per-second accumulator for poison damage-over-time (spike #1320), the counterpart of
        /// <see cref="BleedDamagePerSecond"/> for the poison type. Appended after the amp/resist block rather
        /// than re-homed next to bleed because the attribute enum is intrinsic, DB-backed reference data and
        /// grows append-only; the end-of-tick DoT phase iterates a fixed type list, so the enum order is
        /// immaterial to parity.
        /// </summary>
        PoisonDamagePerSecond = 37,

        /// <summary>
        /// A per-second accumulator for burn damage-over-time (spike #1320), the counterpart of
        /// <see cref="BleedDamagePerSecond"/> for the burn type. Burn resists/amplifies as fire + elemental +
        /// dot through the <see cref="Attributes.DamageTypes.Applies(EDamageType)"/> map.
        /// </summary>
        BurnDamagePerSecond = 38,

        /// <summary>
        /// A derived game attribute. The percentage of a direct hit's post-mitigation damage returned to the
        /// attacker, bypassing the attacker's own mitigation (spike #1330). Decimal-percentage (0.30 = 30%),
        /// base 0 and <b>authored-only</b> — granted by gear/mods/proficiency/class, never derived from a core
        /// attribute — so it is a deliberate build identity (the tank's deterministic kill condition) rather
        /// than a stat tax. Deterministic (no proc) and scoped to direct hits; DoT is never reflected. See
        /// <see cref="Battle.BattleContext.DamageTarget"/>.
        /// </summary>
        DamageReflection = 39,
    }

    /// <summary>
    /// A leaf damage type carried by a single damage instance (skill hit or DoT tick). Exactly one leaf type
    /// per instance; the cross-cutting categories an instance also belongs to (Elemental / DoT) are resolved
    /// through the static <see cref="Attributes.DamageTypes.Applies(EDamageType)"/> map rather than stored.
    /// Spike #1320.
    /// </summary>
    [ClientMirrored]
    public enum EDamageType
    {
        /// <summary>Physical damage. Belongs to no cross-cutting category.</summary>
        Physical = 0,

        /// <summary>Fire damage. Elemental.</summary>
        Fire = 1,

        /// <summary>Water damage. Elemental.</summary>
        Water = 2,

        /// <summary>Earth damage. Elemental.</summary>
        Earth = 3,

        /// <summary>Wind damage. Elemental.</summary>
        Wind = 4,

        /// <summary>Bleed damage-over-time. DoT.</summary>
        Bleed = 5,

        /// <summary>Poison damage-over-time. DoT.</summary>
        Poison = 6,

        /// <summary>Burn damage-over-time. Fire + Elemental + DoT (a burning ember is still fire).</summary>
        Burn = 7,
    }

    /// <summary>
    /// A key that an amplification / resistance attribute is bucketed under (spike #1320). The superset of the
    /// eight leaf <see cref="EDamageType"/> values plus the two cross-cutting categories (<see cref="Elemental"/>,
    /// <see cref="Dot"/>). Each key backs one <c>…Amplification</c> and one <c>…Resistance</c>
    /// <see cref="EAttribute"/>; the static <see cref="Attributes.DamageTypes.Applies(EDamageType)"/> map resolves
    /// a leaf type to the keys whose amp/resist apply to it, in the fixed iteration order parity depends on.
    /// </summary>
    [ClientMirrored]
    public enum EDamageTypeKey
    {
        /// <summary>The physical leaf type.</summary>
        Physical = 0,

        /// <summary>The fire leaf type.</summary>
        Fire = 1,

        /// <summary>The water leaf type.</summary>
        Water = 2,

        /// <summary>The earth leaf type.</summary>
        Earth = 3,

        /// <summary>The wind leaf type.</summary>
        Wind = 4,

        /// <summary>The bleed leaf type.</summary>
        Bleed = 5,

        /// <summary>The poison leaf type.</summary>
        Poison = 6,

        /// <summary>The burn leaf type.</summary>
        Burn = 7,

        /// <summary>The elemental category (fire / water / earth / wind, not physical).</summary>
        Elemental = 8,

        /// <summary>The damage-over-time category (bleed / poison / burn).</summary>
        Dot = 9,
    }

    /// <summary>
    /// The routing key a proficiency <see cref="Proficiencies.Path"/> trains on (spike #1318). A path declares
    /// exactly one; a battle quantity trains the path(s) its key resolves to via the effect-based accrual. The
    /// set spans both books: the ten <em>offense</em> keys (the output book — per-type damage dealt, mirroring
    /// <see cref="EDamageTypeKey"/>'s eight leaf types plus the Elemental/DoT categories), the four combat-event
    /// keys (crit, dodge, heal, reflect) that are not damage types, and the ten <em>resist</em> keys (the
    /// incoming book — per-type pre-mitigation exposure, spike #1338). Offense and resist both route from a
    /// resolved <see cref="EDamageType"/> through <see cref="Attributes.DamageTypes.Applies(EDamageType)"/>,
    /// mapped to the offense or resist key respectively by <see cref="Proficiencies.ActivityKeys"/>. Accrual is
    /// computed server-side at battle completion, off the deterministic tick loop, so this enum is deliberately
    /// <em>not</em> <c>[ClientMirrored]</c> (it carries no battle-parity surface); it reaches the client only as
    /// the <c>Path</c> contract's field, emitted by the reflection-walk codegen like any other contract enum.
    /// </summary>
    public enum EActivityKey
    {
        /// <summary>Physical damage dealt. Mirrors <see cref="EDamageTypeKey.Physical"/>.</summary>
        Physical = 0,

        /// <summary>Fire damage dealt. Mirrors <see cref="EDamageTypeKey.Fire"/>.</summary>
        Fire = 1,

        /// <summary>Water damage dealt. Mirrors <see cref="EDamageTypeKey.Water"/>.</summary>
        Water = 2,

        /// <summary>Earth damage dealt. Mirrors <see cref="EDamageTypeKey.Earth"/>.</summary>
        Earth = 3,

        /// <summary>Wind damage dealt. Mirrors <see cref="EDamageTypeKey.Wind"/>.</summary>
        Wind = 4,

        /// <summary>Bleed damage dealt. Mirrors <see cref="EDamageTypeKey.Bleed"/>.</summary>
        Bleed = 5,

        /// <summary>Poison damage dealt. Mirrors <see cref="EDamageTypeKey.Poison"/>.</summary>
        Poison = 6,

        /// <summary>Burn damage dealt. Mirrors <see cref="EDamageTypeKey.Burn"/>.</summary>
        Burn = 7,

        /// <summary>All elemental (fire / water / earth / wind) damage dealt. Mirrors <see cref="EDamageTypeKey.Elemental"/>.</summary>
        Elemental = 8,

        /// <summary>All damage-over-time (bleed / poison / burn) damage dealt. Mirrors <see cref="EDamageTypeKey.Dot"/>.</summary>
        Dot = 9,

        // The four combat-event keys — not damage types, so absent from EDamageTypeKey. The proc / heal bindings
        // (#1339) wire crit / dodge / heal; Reflect stays inert until the reflection rework (#1330) produces a
        // reflected-damage signal. A path keyed on an unwired key simply accrues nothing until its binding lands.

        /// <summary>Critical damage dealt (the Precision mastery).</summary>
        Crit = 10,

        /// <summary>Damage dodged (the Evasion mastery).</summary>
        Dodge = 11,

        /// <summary>Healing done (the Restoration mastery).</summary>
        Heal = 12,

        /// <summary>Reflected damage (the Retribution mastery). Inert until the reflection rework #1330.</summary>
        Reflect = 13,

        // The ten resist keys — the incoming-book counterparts of the ten damage-type keys above (spike #1338).
        // A path keyed on one trains on the player's pre-mitigation exposure to that type's hits/DoT, routed
        // through the same Applies(type) map on the incoming side (a fire hit trains FireResist and
        // ElementalResist). Appended after the offense + event keys so the existing ordinals — persisted on the
        // Path.ActivityKey column — are untouched. ActivityKeys.ForDamageKeyResist pins each to its damage-key.

        /// <summary>Pre-mitigation physical exposure. Incoming counterpart of <see cref="Physical"/>.</summary>
        PhysicalResist = 14,

        /// <summary>Pre-mitigation fire exposure. Incoming counterpart of <see cref="Fire"/>.</summary>
        FireResist = 15,

        /// <summary>Pre-mitigation water exposure. Incoming counterpart of <see cref="Water"/>.</summary>
        WaterResist = 16,

        /// <summary>Pre-mitigation earth exposure. Incoming counterpart of <see cref="Earth"/>.</summary>
        EarthResist = 17,

        /// <summary>Pre-mitigation wind exposure. Incoming counterpart of <see cref="Wind"/>.</summary>
        WindResist = 18,

        /// <summary>Pre-mitigation bleed exposure. Incoming counterpart of <see cref="Bleed"/>.</summary>
        BleedResist = 19,

        /// <summary>Pre-mitigation poison exposure. Incoming counterpart of <see cref="Poison"/>.</summary>
        PoisonResist = 20,

        /// <summary>Pre-mitigation burn exposure. Incoming counterpart of <see cref="Burn"/>.</summary>
        BurnResist = 21,

        /// <summary>Pre-mitigation elemental exposure. Incoming counterpart of <see cref="Elemental"/>.</summary>
        ElementalResist = 22,

        /// <summary>Pre-mitigation damage-over-time exposure. Incoming counterpart of <see cref="Dot"/>.</summary>
        DotResist = 23,
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
        /// An aggregate stat computed from a base/derived formula (MaxHealth, Toughness, CooldownRecovery,
        /// the crit/dodge set), plus the authored-only DamageReflection (base 0, no derivation).
        /// </summary>
        Secondary = 2,

        /// <summary>
        /// A transient per-second combat channel fed only by effects/gear (the typed DoT accumulators
        /// Bleed/Poison/Burn DamagePerSecond and HealthRegenPerSecond).
        /// </summary>
        Status = 3,

        /// <summary>
        /// A damage-type amplification or resistance attribute (spike #1320) — authored-only, base 0, grouped by
        /// damage-type key for the breakdown screen. Kept distinct from <see cref="Secondary"/> (derived
        /// aggregates) so the large amp/resist family does not crowd that bucket.
        /// </summary>
        Affinity = 4,
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
        // 20 and 21 are retired (the former AttacksBlocked / DamageBlocked, removed with the Block mechanic in
        // spike #1330). The enum is intrinsic, DB-backed reference data, so the ordinals are left as a gap.
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

        /// <summary>Allowed to be the result of a skill-synthesis recipe (authoring intent; spike #1125). Once
        /// synthesized the skill is a permanent player grant like any other, but this flag gates which skills an
        /// authored <c>SkillRecipe</c> may produce.</summary>
        Synthesis = 8,
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
