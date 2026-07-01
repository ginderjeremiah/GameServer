using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a class in the reference-data catalogue — the character-creation preset
    /// (starter kit, attribute fingerprint, signature passive) shared by the game client's create-character
    /// screen and the admin Workbench. The signature passive is flattened to scalar fields so the Workbench
    /// edits it through a plain fields section.</summary>
    public class Class : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Word { get; set; }

        /// <summary>The attribute the signature passive boosts.</summary>
        public EAttribute PassiveAttributeId { get; set; }

        /// <summary>The flat amount of the signature passive.</summary>
        public decimal PassiveAmount { get; set; }

        /// <summary>Optional attribute the signature passive scales off; null when the passive is purely flat.</summary>
        public EAttribute? PassiveScalingAttributeId { get; set; }

        /// <summary>The per-point scaling amount applied to <see cref="PassiveScalingAttributeId"/> (0 when flat).</summary>
        public decimal PassiveScalingAmount { get; set; }

        /// <summary>How the signature passive is applied.</summary>
        public EModifierType PassiveModifierType { get; set; }

        public required IEnumerable<int> StarterSkillIds { get; set; }
        public required IEnumerable<ClassStarterEquipment> StarterEquipment { get; set; }
        public required IEnumerable<AttributeDistribution> AttributeDistributions { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation for new characters but kept resolvable
        /// by id). Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
