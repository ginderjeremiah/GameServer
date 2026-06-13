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
        /// Creates a new attribute based on the given enum value.
        /// </summary>
        /// <param name="id"></param>
        public Attribute(EAttribute id)
        {
            Id = id;
            Name = id.ToString().SpaceWords();
            Description = GetDescription(id);
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
