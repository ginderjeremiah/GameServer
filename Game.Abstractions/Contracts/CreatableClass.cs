using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>A class as a character-creation option: the data the create-character class picker needs
    /// to preview the kit and let the player choose. Purpose-built and kept distinct from the reference
    /// <see cref="Class"/> catalogue — it is delivered over HTTP (<c>Players/CharacterCreationData</c>) so it
    /// is reachable on the pre-selection create-character screens, where the socket (and the reference data
    /// it serves) is not, and it carries resolved skill/item names so the preview needs no other reference
    /// sets. Retired classes are excluded.</summary>
    public class CreatableClass : IModel
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

        /// <summary>The class's level-scaled attribute fingerprint (the locked base).</summary>
        public required IEnumerable<AttributeDistribution> AttributeDistributions { get; set; }

        public required IEnumerable<CreatableClassSkill> StarterSkills { get; set; }
        public required IEnumerable<CreatableClassEquipment> StarterEquipment { get; set; }
    }
}
