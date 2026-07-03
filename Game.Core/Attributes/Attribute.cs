using static Game.Core.EAttribute;

namespace Game.Core.Attributes
{
    /// <inheritdoc cref="EAttribute"/>
    public class Attribute
    {
        /// <summary>
        /// The enum value of the attribute.
        /// </summary>
        public EAttribute Id { get; }

        /// <summary>
        /// The name of the attribute.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// A text description of what the attribute represents.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// The display/classification taxonomy for the attribute (Primary / Secondary / Status).
        /// Display-only; distinct from the <see cref="IsCore"/> power-calc invariant.
        /// </summary>
        public EAttributeType AttributeType { get; }

        /// <summary>
        /// Whether the attribute's value reads as a percentage (e.g. <see cref="EAttribute.CooldownRecovery"/>
        /// and the crit/dodge/block rates). A display flag only.
        /// </summary>
        public bool IsPercentage { get; }

        /// <summary>
        /// Whether raising the attribute is detrimental to its bearer (the typed DoT accumulators
        /// Bleed/Poison/Burn DamagePerSecond). Drives buff/debuff tinting; never used in battle math, so it is
        /// parity-safe.
        /// </summary>
        public bool IsHarmful { get; }

        /// <summary>
        /// The short code for the attribute (e.g. <c>STR</c>), or empty for attributes without a
        /// conventional code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// The canonical ordering of the attribute within the UI.
        /// </summary>
        public int DisplayOrder { get; }

        /// <summary>
        /// The number of decimal places the value should be displayed with in its natural display form
        /// (the percentage value for <see cref="IsPercentage"/> attributes). All attributes render as
        /// whole numbers today, but a fractional attribute would raise it.
        /// </summary>
        public int Decimals { get; }

        /// <summary>
        /// The damage-type key this is an amplification/resistance attribute for (spike #1320), or
        /// <c>null</c> for every other attribute. Lets the breakdown screen group the amp/resist family
        /// by damage type. A display tag only; never used in battle math, so it is parity-safe.
        /// </summary>
        public EDamageTypeKey? DamageTypeKey { get; }

        /// <summary>
        /// Creates a new attribute based on the given enum value.
        /// </summary>
        /// <param name="id"></param>
        public Attribute(EAttribute id)
        {
            Id = id;
            Name = id.ToString().SpaceWords();
            Description = GetDescription(id);
            var metadata = GetDisplayMetadata(id);
            AttributeType = metadata.Type;
            IsPercentage = metadata.IsPercentage;
            IsHarmful = metadata.IsHarmful;
            Code = metadata.Code;
            DisplayOrder = metadata.DisplayOrder;
            Decimals = metadata.Decimals;
            DamageTypeKey = DamageTypes.KeyForAttribute(id);
        }

        private static string GetDescription(EAttribute value)
        {
            // The 20 damage-type amplification/resistance attributes (spike #1320) are templated from their
            // key's foundation facts rather than hand-listed, so the family scales as damage types are added.
            if (DamageTypes.KeyForAttribute(value) is EDamageTypeKey ampResistKey)
            {
                var info = DamageTypes.Info(ampResistKey);
                return info.Resistance == value
                    ? $"Reduces the damage you take from {info.Label} attacks."
                    : $"Increases the damage your {info.Label} attacks deal.";
            }

#pragma warning disable CS0618 // DropBonus is obsolete but still seeded for data integrity.
            return value switch
            {
                Strength => "A measure of one's raw physical force. Increases the damage of some physical skills and contributes to maximum health.",
                Endurance => "A measure of one's resilience and physical fortitude. Contributes to maximum health and toughness.",
                Intellect => "A measure of one's mental acuity and command of the arcane. Increases the damage of magical skills.",
                Agility => "A measure of one's speed and reflexes. Improves cooldown recovery and amplifies dodge chance.",
                Dexterity => "A measure of one's precision and finesse. Increases the damage of some physical skills and improves cooldown recovery.",
                Luck => "A measure of one's fortune, influencing various chance-based outcomes.",
                MaxHealth => "The amount of health a character has at the start of a battle.",
                Toughness => "Reduces all incoming direct damage by a percentage that grows with diminishing returns, never reaching full immunity.",
                CooldownRecovery => "A percentage multiplier to the rate at which skills become available again after being used.",
                DropBonus => "Obsolete. Previously increased the rate at which items were dropped by enemies.",
                CriticalChanceMultiplier => "A multiplier applied to a skill's own base critical-hit chance.",
                CriticalDamage => "The additional percentage of damage dealt when a critical hit occurs.",
                DodgeChance => "The percentage chance to completely avoid the damage from an incoming attack.",
                DamageReflection => "The percentage of a direct hit's damage returned to the attacker, ignoring their defenses.",
                ExecuteBonus => "The maximum percentage of bonus damage dealt against a target, scaled by how much health it is missing.",
                ParryChance => "The percentage chance to parry an incoming attack, negating it and striking back with the equipped weapon's signature skill.",
                ParryChanceMultiplier => "A multiplier applied to your parry chance.",
                DodgeChanceMultiplier => "A multiplier applied to your dodge chance.",
                BleedDamagePerSecond => "The amount of bleed damage taken each second from damage-over-time effects.",
                PoisonDamagePerSecond => "The amount of poison damage taken each second from damage-over-time effects.",
                BurnDamagePerSecond => "The amount of burn damage taken each second from damage-over-time effects.",
                HealthRegenPerSecond => "The amount of health restored each second from heal-over-time effects.",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "No description defined for the given attribute.")
            };
#pragma warning restore CS0618
        }

        /// <summary>
        /// The per-attribute display/classification metadata, mapped in the same switch style as
        /// <see cref="GetDescription"/>. Columns: (Type, IsPercentage, IsHarmful, Code, DisplayOrder, Decimals).
        /// </summary>
        private static AttributeDisplayMetadata GetDisplayMetadata(EAttribute value)
        {
            // Damage-type amplification/resistance attributes (spike #1320): templated from the key's facts —
            // the Affinity display group, decimal-percentage, harmless, code "<KEY> AMP"/"<KEY> RES", and a
            // display order tracking the enum value so the amp/resist family sorts after the existing set.
            if (DamageTypes.KeyForAttribute(value) is EDamageTypeKey ampResistKey)
            {
                var info = DamageTypes.Info(ampResistKey);
                var code = $"{info.Code} {(info.Resistance == value ? "RES" : "AMP")}";
                return new(EAttributeType.Affinity, IsPercentage: true, IsHarmful: false, code, (int)value, 0);
            }

#pragma warning disable CS0618 // DropBonus is obsolete but still seeded for data integrity.
            // The two adjacent bools are named at every call site so a future arm cannot silently
            // transpose IsPercentage/IsHarmful (the enum/string/int columns are type-distinct).
            return value switch
            {
                Strength => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "STR", 0, 0),
                Endurance => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "END", 1, 0),
                Intellect => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "INT", 2, 0),
                Agility => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "AGI", 3, 0),
                Dexterity => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "DEX", 4, 0),
                Luck => new(EAttributeType.Primary, IsPercentage: false, IsHarmful: false, "LUK", 5, 0),
                MaxHealth => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "HP", 6, 0),
                Toughness => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "TGH", 7, 0),
                CooldownRecovery => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "CDR", 8, 0),
                CriticalChanceMultiplier => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "CRT", 9, 0),
                CriticalDamage => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "CRT DMG", 10, 0),
                DodgeChance => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "DOD", 11, 0),
                // DamageReflection (spike #1330) takes the defensive-secondary slot Block vacated, so it groups
                // with the other combat-secondary stats on the breakdown screen. Authored-only, decimal-percentage.
                DamageReflection => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "REF", 12, 0),
                // ExecuteBonus (spike #1398, #1430) is the Cull archetype's enabler — authored-only like
                // DamageReflection, grouped with the other combat-secondary stats.
                ExecuteBonus => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "EXE", 13, 0),
                BleedDamagePerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: true, "BLD DOT", 14, 0),
                HealthRegenPerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: false, "REG", 15, 0),
                // Obsolete: never displayed, so it gets neutral display metadata and sorts last.
                DropBonus => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "DRP", 16, 0),
                // The poison/burn accumulators sort after the amp/resist block (display order = enum value, the
                // same convention that block uses) since they append after it; bleed keeps the former DoT slot.
                PoisonDamagePerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: true, "PSN DOT", 37, 0),
                BurnDamagePerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: true, "BRN DOT", 38, 0),
                // The parry pair (#1457) appends after the amp/resist block like the accumulators above
                // (display order = enum value): combat secondaries like DodgeChance/ExecuteBonus.
                ParryChance => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "PRY", 47, 0),
                ParryChanceMultiplier => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "PRY MULT", 48, 0),
                DodgeChanceMultiplier => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "DOD MULT", 49, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(value), value, "No display metadata defined for the given attribute.")
            };
#pragma warning restore CS0618
        }

        /// <summary>
        /// The display/classification facts for a single attribute, resolved by
        /// <see cref="GetDisplayMetadata"/> and projected onto the attribute's display properties.
        /// </summary>
        private readonly record struct AttributeDisplayMetadata(
            EAttributeType Type,
            bool IsPercentage,
            bool IsHarmful,
            string Code,
            int DisplayOrder,
            int Decimals);

        public static IEnumerable<Attribute> GetAllAttributes()
        {
            return Enum.GetValues<EAttribute>().Select(a => new Attribute(a));
        }

        /// <summary>
        /// The core attributes a player directly invests stat points into. Every other attribute is
        /// "derived" — computed from these via <see cref="Modifiers.StaticAttributeModifiers"/> — so the
        /// core set is the meaningful measure of raw attribute investment.
        /// </summary>
        private static readonly HashSet<EAttribute> CoreAttributeSet =
        [
            Strength, Endurance, Intellect, Agility, Dexterity, Luck,
        ];

        /// <inheritdoc cref="CoreAttributeSet"/>
        public static IReadOnlySet<EAttribute> CoreAttributes => CoreAttributeSet;

        /// <summary>
        /// Whether the given <paramref name="attribute"/> is a core (directly-allocatable) attribute
        /// rather than a derived one.
        /// </summary>
        public static bool IsCore(EAttribute attribute)
        {
            return CoreAttributeSet.Contains(attribute);
        }
    }
}
