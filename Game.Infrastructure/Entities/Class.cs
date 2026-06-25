namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A class: the character-creation preset that gives a new character its starting identity — a themed
    /// starter kit (skills + equipped gear), a level-scaled attribute fingerprint (the locked base), and a
    /// durable signature passive (see the class-system spike, <c>docs/spikes/1126-class-system.md</c>).
    /// Static, authored reference data with a zero-based identity. This entity carries only the authored
    /// definition; the creation/battler wiring that consumes it lands in later sub-issues (#1221–#1225).
    /// </summary>
    public class Class : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>The conlang "word of power" decorative label (rendered via the WordOfPower component). A
        /// class reuses the script as a flat decorative label — unlike proficiencies it does not decipher.</summary>
        public required string Word { get; set; }

        /// <summary>The attribute the signature passive boosts (an <see cref="Core.EAttribute"/>). Stored as a
        /// plain value (no FK) like other enum-backed columns; the authored value is constrained to the enum.</summary>
        public int PassiveAttributeId { get; set; }

        /// <summary>The flat amount of the signature passive.</summary>
        public decimal PassiveAmount { get; set; }

        /// <summary>Optional attribute the signature passive scales off (e.g. Mage effect-magnitude with
        /// Intellect), mirroring skill-effect scaling. Null when the passive is purely flat.</summary>
        public int? PassiveScalingAttributeId { get; set; }

        /// <summary>The per-point scaling amount applied to <see cref="PassiveScalingAttributeId"/> (0 when flat).</summary>
        public decimal PassiveScalingAmount { get; set; }

        /// <summary>How the signature passive is applied (an <see cref="Core.EModifierType"/>).</summary>
        public int PassiveModifierType { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>). A retired
        /// class is out of circulation for new character creation but still resolves by id for existing
        /// characters that chose it.</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<ClassStarterSkill> StarterSkills { get => field ?? throw new NotLoadedException(nameof(StarterSkills)); set; }
        public virtual List<ClassStarterEquipment> StarterEquipment { get => field ?? throw new NotLoadedException(nameof(StarterEquipment)); set; }
        public virtual List<ClassAttributeDistribution> AttributeDistributions { get => field ?? throw new NotLoadedException(nameof(AttributeDistributions)); set; }
    }
}
