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
        /// Whether raising the attribute is detrimental to its bearer (only
        /// <see cref="EAttribute.DamageTakenPerSecond"/> today). Drives buff/debuff tinting; never used in
        /// battle math, so it is parity-safe.
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
        /// The number of decimal places the value should be displayed with (e.g.
        /// <see cref="EAttribute.CooldownRecovery"/> = 2, most = 0).
        /// </summary>
        public int Decimals { get; }

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
        }

        private static string GetDescription(EAttribute value)
        {
#pragma warning disable CS0618 // DropBonus is obsolete but still seeded for data integrity.
            return value switch
            {
                Strength => "A measure of one's raw physical force. Increases the damage of some physical skills and contributes to maximum health.",
                Endurance => "A measure of one's resilience and physical fortitude. Contributes to maximum health and defense.",
                Intellect => "A measure of one's mental acuity and command of the arcane. Increases the damage of magical skills.",
                Agility => "A measure of one's speed and reflexes. Improves cooldown recovery and contributes to defense.",
                Dexterity => "A measure of one's precision and finesse. Increases the damage of some physical skills and improves cooldown recovery.",
                Luck => "A measure of one's fortune, influencing various chance-based outcomes.",
                MaxHealth => "The amount of health a character has at the start of a battle.",
                Defense => "A flat reduction applied to all incoming damage.",
                CooldownRecovery => "A percentage multiplier to the rate at which skills become available again after being used.",
                DropBonus => "Obsolete. Previously increased the rate at which items were dropped by enemies.",
                CriticalChance => "The percentage chance for an attack to deal increased damage.",
                CriticalDamage => "The additional percentage of damage dealt when a critical hit occurs.",
                DodgeChance => "The percentage chance to completely avoid the damage from an incoming attack.",
                BlockChance => "The percentage chance to block part of the damage from an incoming attack.",
                BlockReduction => "A flat reduction applied to damage received when an attack is blocked.",
                DamageTakenPerSecond => "The amount of damage taken each second from damage-over-time effects.",
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
                MaxHealth => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "", 6, 0),
                Defense => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "", 7, 0),
                CooldownRecovery => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "", 8, 2),
                CriticalChance => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "", 9, 0),
                CriticalDamage => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "", 10, 0),
                DodgeChance => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "", 11, 0),
                BlockChance => new(EAttributeType.Secondary, IsPercentage: true, IsHarmful: false, "", 12, 0),
                BlockReduction => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "", 13, 0),
                DamageTakenPerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: true, "", 14, 0),
                HealthRegenPerSecond => new(EAttributeType.Status, IsPercentage: false, IsHarmful: false, "", 15, 0),
                // Obsolete: never displayed, so it gets neutral display metadata and sorts last.
                DropBonus => new(EAttributeType.Secondary, IsPercentage: false, IsHarmful: false, "", 16, 0),
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
