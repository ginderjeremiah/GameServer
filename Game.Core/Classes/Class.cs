using Game.Core.Attributes;

namespace Game.Core.Classes
{
    /// <summary>
    /// The lean, immutable domain model for a class — the character-creation preset (see the class-system
    /// spike, <c>docs/spikes/1126-class-system.md</c>). Shared, pre-materialized and reused by reference from
    /// the reference-data cache, so it is structurally immutable (init-only, read-only collections). Carries
    /// only what gameplay needs; the creation/battler wiring that consumes it lands in later sub-issues
    /// (#1221–#1225).
    /// </summary>
    public sealed class Class
    {
        public required int Id { get; init; }
        public required string Name { get; init; }

        /// <summary>The skills the class grants (selected) at character creation.</summary>
        public required IReadOnlyList<int> StarterSkillIds { get; init; }

        /// <summary>The items the class starts with equipped.</summary>
        public required IReadOnlyList<ClassStarterEquipment> StarterEquipment { get; init; }

        /// <summary>The level-scaled, non-reallocatable attribute fingerprint (the locked base).</summary>
        public required IReadOnlyList<AttributeDistribution> AttributeDistributions { get; init; }

        /// <summary>The class's signature passive.</summary>
        public required ClassSignaturePassive SignaturePassive { get; init; }
    }
}
